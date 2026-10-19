using System.Buffers.Binary;
using System.Text;
using Utilities.Infrastructure.External.Conversion;
using Xunit;

namespace Utilities.Infrastructure.Tests.External.Conversion;

public sealed class GlbValidatorTests
{
    [Fact]
    public async Task ValidateAsync_InterleavedLinePositionsInBinChunk_Succeeds()
    {
        using var files = new TemporaryDirectory();
        var path = files.GetPath("lines.glb");
        var json = CreateJson(
            bufferLength: 32,
            viewOffset: 4,
            viewLength: 28,
            positionCount: 2,
            positionType: "VEC3",
            componentType: 5126,
            byteStride: 16,
            primitiveMode: 1);
        WriteGlb(path, json, new byte[32]);

        await GlbValidator.ValidateAsync(path, CancellationToken.None);
    }

    [Fact]
    public async Task ValidateAsync_MissingBinChunk_Fails()
    {
        using var files = new TemporaryDirectory();
        var path = files.GetPath("missing-bin.glb");
        var json = CreateJson(24, 0, 24, 2, "VEC3", 5126, 12, 1);
        WriteGlb(path, json, binary: null);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GlbValidator.ValidateAsync(path, CancellationToken.None));

        Assert.Contains("BIN chunk", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_DeclaredBufferLongerThanBinChunk_Fails()
    {
        using var files = new TemporaryDirectory();
        var path = files.GetPath("short-bin.glb");
        var json = CreateJson(32, 0, 24, 2, "VEC3", 5126, 12, 1);
        WriteGlb(path, json, new byte[24]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GlbValidator.ValidateAsync(path, CancellationToken.None));

        Assert.Contains("buffer length", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_PositionAccessorOutsideBufferView_Fails()
    {
        using var files = new TemporaryDirectory();
        var path = files.GetPath("position-out-of-range.glb");
        var json = CreateJson(24, 0, 24, 2, "VEC3", 5126, 16, 1);
        WriteGlb(path, json, new byte[24]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GlbValidator.ValidateAsync(path, CancellationToken.None));

        Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("VEC2", 5126)]
    [InlineData("VEC3", 5123)]
    public async Task ValidateAsync_NonFloatVec3PositionAccessor_Fails(
        string positionType,
        int componentType)
    {
        using var files = new TemporaryDirectory();
        var path = files.GetPath("invalid-position-type.glb");
        var json = CreateJson(24, 0, 24, 2, positionType, componentType, 12, 1);
        WriteGlb(path, json, new byte[24]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GlbValidator.ValidateAsync(path, CancellationToken.None));

        Assert.Contains("FLOAT VEC3", exception.Message, StringComparison.Ordinal);
    }

    private static string CreateJson(
        int bufferLength,
        int viewOffset,
        int viewLength,
        int positionCount,
        string positionType,
        int componentType,
        int byteStride,
        int primitiveMode) => $$"""
        {
          "asset": { "version": "2.0" },
          "scene": 0,
          "scenes": [{ "nodes": [0] }],
          "nodes": [{ "mesh": 0 }],
          "meshes": [{
            "primitives": [{
              "attributes": { "POSITION": 0 },
              "mode": {{primitiveMode}}
            }]
          }],
          "buffers": [{ "byteLength": {{bufferLength}} }],
          "bufferViews": [{
            "buffer": 0,
            "byteOffset": {{viewOffset}},
            "byteLength": {{viewLength}},
            "byteStride": {{byteStride}}
          }],
          "accessors": [{
            "bufferView": 0,
            "byteOffset": 0,
            "componentType": {{componentType}},
            "count": {{positionCount}},
            "type": "{{positionType}}"
          }]
        }
        """;

    private static void WriteGlb(string path, string json, byte[]? binary)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var paddedJsonLength = (jsonBytes.Length + 3) & ~3;
        var paddedBinaryLength = binary is null ? 0 : (binary.Length + 3) & ~3;
        var totalLength = 12 + 8 + paddedJsonLength +
            (binary is null ? 0 : 8 + paddedBinaryLength);
        var bytes = new byte[totalLength];

        Encoding.ASCII.GetBytes("glTF").CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), (uint)paddedJsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), 0x4E4F534A);
        jsonBytes.CopyTo(bytes, 20);
        bytes.AsSpan(20 + jsonBytes.Length, paddedJsonLength - jsonBytes.Length).Fill(0x20);

        if (binary is not null)
        {
            var binaryHeaderOffset = 20 + paddedJsonLength;
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(binaryHeaderOffset, 4),
                (uint)paddedBinaryLength);
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(binaryHeaderOffset + 4, 4),
                0x004E4942);
            binary.CopyTo(bytes, binaryHeaderOffset + 8);
        }

        File.WriteAllBytes(path, bytes);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            "utilities-glb-validator-tests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(_path);

        public string GetPath(string fileName) => Path.Combine(_path, fileName);

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, recursive: true);
            }
        }
    }
}
