using System.Buffers.Binary;
using System.Text.Json;

namespace Utilities.Infrastructure.External.Conversion;

public static class GlbValidator
{
    private const uint GlbMagic = 0x46546C67;
    private const uint SupportedVersion = 2;
    private const uint JsonChunkType = 0x4E4F534A;
    private const uint BinaryChunkType = 0x004E4942;
    private const int FloatComponentType = 5126;
    private const int PositionElementSize = 3 * sizeof(float);
    private const int MaximumVertexAttributeStride = 252;
    private const int HeaderLength = 12;
    private const int ChunkHeaderLength = 8;
    private const int MaximumJsonChunkLength = 64 * 1024 * 1024;

    public static async Task ValidateAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (stream.Length < HeaderLength + ChunkHeaderLength)
        {
            throw new InvalidDataException("The GLB output is too short to contain a valid document.");
        }

        var header = new byte[HeaderLength];
        await stream.ReadExactlyAsync(header, cancellationToken);

        if (BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4)) != GlbMagic)
        {
            throw new InvalidDataException("The converter output does not have a GLB header.");
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4)) != SupportedVersion)
        {
            throw new InvalidDataException("The converter output is not a glTF 2.0 binary document.");
        }

        var declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, 4));
        if (declaredLength != stream.Length)
        {
            throw new InvalidDataException("The GLB output length does not match its header.");
        }

        var chunkHeader = new byte[ChunkHeaderLength];
        var chunkIndex = 0;
        byte[]? json = null;
        long? binaryChunkLength = null;

        while (stream.Position < stream.Length)
        {
            if (stream.Length - stream.Position < ChunkHeaderLength)
            {
                throw new InvalidDataException("The GLB output contains a truncated chunk header.");
            }

            await stream.ReadExactlyAsync(chunkHeader, cancellationToken);
            var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.AsSpan(0, 4));
            var chunkType = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.AsSpan(4, 4));

            if (chunkLength % 4 != 0)
            {
                throw new InvalidDataException("The GLB output contains an unaligned chunk.");
            }

            if (chunkLength > stream.Length - stream.Position)
            {
                throw new InvalidDataException("The GLB output contains a truncated chunk.");
            }

            if (chunkIndex == 0 && chunkType != JsonChunkType)
            {
                throw new InvalidDataException("The first GLB chunk is not JSON.");
            }

            if (chunkType == JsonChunkType)
            {
                if (json is not null || chunkIndex != 0)
                {
                    throw new InvalidDataException("The GLB output contains an unexpected JSON chunk.");
                }

                if (chunkLength == 0 || chunkLength > MaximumJsonChunkLength)
                {
                    throw new InvalidDataException("The GLB JSON chunk has an invalid size.");
                }

                json = new byte[(int)chunkLength];
                await stream.ReadExactlyAsync(json, cancellationToken);
            }
            else if (chunkType == BinaryChunkType)
            {
                if (binaryChunkLength is not null)
                {
                    throw new InvalidDataException("The GLB output contains more than one BIN chunk.");
                }

                binaryChunkLength = chunkLength;
                stream.Seek(chunkLength, SeekOrigin.Current);
            }
            else
            {
                stream.Seek(chunkLength, SeekOrigin.Current);
            }

            chunkIndex++;
        }

        if (json is null)
        {
            throw new InvalidDataException("The GLB output does not contain a JSON chunk.");
        }

        if (binaryChunkLength is null)
        {
            throw new InvalidDataException("The GLB output does not contain a BIN chunk.");
        }

        ValidateJson(json, binaryChunkLength.Value);
    }

    private static void ValidateJson(ReadOnlyMemory<byte> json, long binaryChunkLength)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The GLB JSON chunk is malformed.", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("asset", out var asset) ||
                asset.ValueKind != JsonValueKind.Object ||
                !asset.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.String ||
                version.GetString() is not { } versionText ||
                !versionText.StartsWith("2.", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The GLB JSON does not declare glTF 2.x.");
            }

            var declaredBufferLength = ValidateEmbeddedBuffer(root, binaryChunkLength);
            if (!root.TryGetProperty("bufferViews", out var bufferViews) ||
                bufferViews.ValueKind != JsonValueKind.Array ||
                bufferViews.GetArrayLength() == 0 ||
                !root.TryGetProperty("accessors", out var accessors) ||
                accessors.ValueKind != JsonValueKind.Array ||
                !root.TryGetProperty("meshes", out var meshes) ||
                meshes.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("The GLB output does not contain mesh accessors.");
            }

            var drawableMeshes = FindDrawableMeshes(
                meshes,
                accessors,
                bufferViews,
                declaredBufferLength,
                binaryChunkLength);
            if (drawableMeshes.Count == 0)
            {
                throw new InvalidDataException("The GLB output does not contain drawable geometry.");
            }

            if (!HasSceneInstance(root, drawableMeshes))
            {
                throw new InvalidDataException("The GLB output does not instance its drawable geometry in a scene.");
            }
        }
    }

    private static HashSet<int> FindDrawableMeshes(
        JsonElement meshes,
        JsonElement accessors,
        JsonElement bufferViews,
        long declaredBufferLength,
        long binaryChunkLength)
    {
        var drawableMeshes = new HashSet<int>();
        var meshIndex = 0;

        foreach (var mesh in meshes.EnumerateArray())
        {
            if (mesh.ValueKind == JsonValueKind.Object &&
                mesh.TryGetProperty("primitives", out var primitives) &&
                primitives.ValueKind == JsonValueKind.Array)
            {
                var hasPositions = false;
                foreach (var primitive in primitives.EnumerateArray())
                {
                    if (HasPositions(
                            primitive,
                            accessors,
                            bufferViews,
                            declaredBufferLength,
                            binaryChunkLength))
                    {
                        hasPositions = true;
                    }
                }

                if (hasPositions)
                {
                    drawableMeshes.Add(meshIndex);
                }
            }

            meshIndex++;
        }

        return drawableMeshes;
    }

    private static bool HasPositions(
        JsonElement primitive,
        JsonElement accessors,
        JsonElement bufferViews,
        long declaredBufferLength,
        long binaryChunkLength)
    {
        if (primitive.ValueKind != JsonValueKind.Object ||
            !primitive.TryGetProperty("attributes", out var attributes) ||
            attributes.ValueKind != JsonValueKind.Object ||
            !attributes.TryGetProperty("POSITION", out var position))
        {
            return false;
        }

        if (!position.TryGetInt32(out var accessorIndex) ||
            accessorIndex < 0 ||
            accessorIndex >= accessors.GetArrayLength())
        {
            throw new InvalidDataException("A drawable primitive has an invalid POSITION accessor index.");
        }

        var accessor = accessors[accessorIndex];
        ValidatePositionAccessor(
            accessor,
            bufferViews,
            declaredBufferLength,
            binaryChunkLength);
        return true;
    }

    private static long ValidateEmbeddedBuffer(JsonElement root, long binaryChunkLength)
    {
        if (!root.TryGetProperty("buffers", out var buffers) ||
            buffers.ValueKind != JsonValueKind.Array ||
            buffers.GetArrayLength() != 1)
        {
            throw new InvalidDataException("The GLB output must declare exactly one embedded buffer.");
        }

        var buffer = buffers[0];
        if (buffer.ValueKind != JsonValueKind.Object ||
            buffer.TryGetProperty("uri", out _))
        {
            throw new InvalidDataException("The GLB buffer must be embedded in its BIN chunk.");
        }

        var declaredLength = GetRequiredInteger(buffer, "byteLength", "buffer");
        if (declaredLength <= 0 ||
            declaredLength > binaryChunkLength ||
            binaryChunkLength - declaredLength > 3)
        {
            throw new InvalidDataException("The GLB buffer length does not match its BIN chunk.");
        }

        return declaredLength;
    }

    private static void ValidatePositionAccessor(
        JsonElement accessor,
        JsonElement bufferViews,
        long declaredBufferLength,
        long binaryChunkLength)
    {
        if (accessor.ValueKind != JsonValueKind.Object ||
            !accessor.TryGetProperty("type", out var type) ||
            type.ValueKind != JsonValueKind.String ||
            !string.Equals(type.GetString(), "VEC3", StringComparison.Ordinal) ||
            !accessor.TryGetProperty("componentType", out var componentType) ||
            !componentType.TryGetInt32(out var componentTypeValue) ||
            componentTypeValue != FloatComponentType)
        {
            throw new InvalidDataException("A POSITION accessor must contain FLOAT VEC3 data.");
        }

        if (accessor.TryGetProperty("normalized", out var normalized) &&
            normalized.ValueKind != JsonValueKind.False)
        {
            throw new InvalidDataException("A FLOAT POSITION accessor cannot be normalized.");
        }

        var count = GetRequiredInteger(accessor, "count", "POSITION accessor");
        if (count <= 0)
        {
            throw new InvalidDataException("A POSITION accessor must contain at least one vertex.");
        }

        if (!accessor.TryGetProperty("bufferView", out var bufferViewProperty) ||
            !bufferViewProperty.TryGetInt32(out var bufferViewIndex) ||
            bufferViewIndex < 0 ||
            bufferViewIndex >= bufferViews.GetArrayLength())
        {
            throw new InvalidDataException("A POSITION accessor has an invalid bufferView index.");
        }

        var bufferView = bufferViews[bufferViewIndex];
        if (bufferView.ValueKind != JsonValueKind.Object ||
            !bufferView.TryGetProperty("buffer", out var bufferProperty) ||
            !bufferProperty.TryGetInt32(out var bufferIndex) ||
            bufferIndex != 0)
        {
            throw new InvalidDataException("A POSITION bufferView must reference the embedded GLB buffer.");
        }

        var accessorOffset = GetOptionalInteger(accessor, "byteOffset", "POSITION accessor");
        var viewOffset = GetOptionalInteger(bufferView, "byteOffset", "POSITION bufferView");
        var viewLength = GetRequiredInteger(bufferView, "byteLength", "POSITION bufferView");
        var stride = bufferView.TryGetProperty("byteStride", out _)
            ? GetRequiredInteger(bufferView, "byteStride", "POSITION bufferView")
            : PositionElementSize;

        if (accessorOffset % sizeof(float) != 0 || viewOffset % sizeof(float) != 0)
        {
            throw new InvalidDataException("POSITION data offsets must be four-byte aligned.");
        }

        if (viewLength <= 0 ||
            stride < PositionElementSize ||
            stride > MaximumVertexAttributeStride ||
            stride % sizeof(float) != 0)
        {
            throw new InvalidDataException("The POSITION bufferView has an invalid byteLength or byteStride.");
        }

        long accessorEnd;
        long viewEnd;
        try
        {
            accessorEnd = checked(accessorOffset + checked((count - 1) * stride) + PositionElementSize);
            viewEnd = checked(viewOffset + viewLength);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("The POSITION accessor byte range overflows.", exception);
        }

        if (accessorEnd > viewLength ||
            viewEnd > declaredBufferLength ||
            viewEnd > binaryChunkLength)
        {
            throw new InvalidDataException("The POSITION accessor points outside the embedded BIN data.");
        }
    }

    private static long GetRequiredInteger(JsonElement element, string propertyName, string owner)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            !property.TryGetInt64(out var value) ||
            value < 0)
        {
            throw new InvalidDataException($"The {owner} has an invalid {propertyName}.");
        }

        return value;
    }

    private static long GetOptionalInteger(JsonElement element, string propertyName, string owner) =>
        element.TryGetProperty(propertyName, out _)
            ? GetRequiredInteger(element, propertyName, owner)
            : 0;

    private static bool HasSceneInstance(JsonElement root, IReadOnlySet<int> drawableMeshes)
    {
        if (!root.TryGetProperty("nodes", out var nodes) ||
            nodes.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("scenes", out var scenes) ||
            scenes.ValueKind != JsonValueKind.Array ||
            scenes.GetArrayLength() == 0)
        {
            return false;
        }

        var sceneIndex = 0;
        if (root.TryGetProperty("scene", out var selectedScene) &&
            (!selectedScene.TryGetInt32(out sceneIndex) || sceneIndex < 0))
        {
            return false;
        }

        if (sceneIndex >= scenes.GetArrayLength())
        {
            return false;
        }

        var scene = scenes[sceneIndex];
        if (scene.ValueKind != JsonValueKind.Object ||
            !scene.TryGetProperty("nodes", out var rootNodes) ||
            rootNodes.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var visited = new HashSet<int>();
        foreach (var node in rootNodes.EnumerateArray())
        {
            if (node.TryGetInt32(out var nodeIndex) &&
                IsDrawableNode(nodeIndex, nodes, drawableMeshes, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDrawableNode(
        int nodeIndex,
        JsonElement nodes,
        IReadOnlySet<int> drawableMeshes,
        ISet<int> visited)
    {
        if (nodeIndex < 0 ||
            nodeIndex >= nodes.GetArrayLength() ||
            !visited.Add(nodeIndex))
        {
            return false;
        }

        var node = nodes[nodeIndex];
        if (node.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (node.TryGetProperty("mesh", out var mesh) &&
            mesh.TryGetInt32(out var meshIndex) &&
            drawableMeshes.Contains(meshIndex))
        {
            return true;
        }

        if (!node.TryGetProperty("children", out var children) ||
            children.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var child in children.EnumerateArray())
        {
            if (child.TryGetInt32(out var childIndex) &&
                IsDrawableNode(childIndex, nodes, drawableMeshes, visited))
            {
                return true;
            }
        }

        return false;
    }
}
