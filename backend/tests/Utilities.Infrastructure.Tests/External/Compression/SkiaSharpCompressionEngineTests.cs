using SkiaSharp;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.External.Compression;
using Xunit;

namespace Utilities.Infrastructure.Tests.External.Compression;

public sealed class SkiaSharpCompressionEngineTests
{
    [Fact]
    public async Task CompressAsync_PngInput_WritesDecodableWebpAtRequestedQuality()
    {
        using var files = new TemporaryDirectory();
        var inputPath = Path.Combine(files.Path, "source.png");
        var outputPath = Path.Combine(files.Path, "compressed.webp");
        WriteImage(inputPath, SKEncodedImageFormat.Png, 32, 24);

        var task = CreateTask("png", "webp", 65);
        var progress = new List<double>();

        var result = await new SkiaSharpCompressionEngine().CompressAsync(
            task,
            inputPath,
            outputPath,
            progress.Add,
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.True(result.CompressedSizeBytes > 0);
        Assert.True(File.Exists(outputPath));
        using var codec = SKCodec.Create(outputPath);
        Assert.NotNull(codec);
        Assert.Equal(32, codec.Info.Width);
        Assert.Equal(24, codec.Info.Height);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        Assert.Equal([10d, 25d, 55d, 90d], progress);
    }

    [Fact]
    public async Task CompressAsync_PreservedPng_UsesLosslessPngEncoding()
    {
        using var files = new TemporaryDirectory();
        var inputPath = Path.Combine(files.Path, "source.png");
        var outputPath = Path.Combine(files.Path, "compressed.png");
        WritePatternPng(inputPath, width: 12, height: 9);

        var result = await new SkiaSharpCompressionEngine().CompressAsync(
            CreateTask("png", "png", 100),
            inputPath,
            outputPath,
            _ => { },
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        using var outputCodec = SKCodec.Create(outputPath);
        Assert.NotNull(outputCodec);
        Assert.Equal(SKEncodedImageFormat.Png, outputCodec.EncodedFormat);

        using var inputBitmap = SKBitmap.Decode(inputPath);
        using var outputBitmap = SKBitmap.Decode(outputPath);
        Assert.NotNull(inputBitmap);
        Assert.NotNull(outputBitmap);
        Assert.Equal(inputBitmap.Width, outputBitmap.Width);
        Assert.Equal(inputBitmap.Height, outputBitmap.Height);
        for (var y = 0; y < inputBitmap.Height; y++)
        {
            for (var x = 0; x < inputBitmap.Width; x++)
            {
                Assert.Equal(inputBitmap.GetPixel(x, y), outputBitmap.GetPixel(x, y));
            }
        }
    }

    [Fact]
    public async Task CompressAsync_InvalidContent_ReturnsFailureWithoutThrowing()
    {
        using var files = new TemporaryDirectory();
        var inputPath = Path.Combine(files.Path, "source.jpg");
        var outputPath = Path.Combine(files.Path, "compressed.jpg");
        await File.WriteAllTextAsync(inputPath, "not an image");

        var result = await new SkiaSharpCompressionEngine().CompressAsync(
            CreateTask("jpg", "jpg", 80),
            inputPath,
            outputPath,
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task CompressAsync_ContentDoesNotMatchExtension_ReturnsFailure()
    {
        using var files = new TemporaryDirectory();
        var inputPath = Path.Combine(files.Path, "renamed.jpg");
        var outputPath = Path.Combine(files.Path, "compressed.jpg");
        WriteImage(inputPath, SKEncodedImageFormat.Png, 8, 8);

        var result = await new SkiaSharpCompressionEngine().CompressAsync(
            CreateTask("jpg", "jpg", 80),
            inputPath,
            outputPath,
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("The image content does not match its file extension.", result.Error);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task CompressAsync_JpegWithExifRotation_AppliesOrientation()
    {
        using var files = new TemporaryDirectory();
        var inputPath = Path.Combine(files.Path, "rotated.jpg");
        var outputPath = Path.Combine(files.Path, "compressed.jpg");
        WriteJpegWithOrientation(inputPath, width: 11, height: 7);

        using (var inputCodec = SKCodec.Create(inputPath))
        {
            Assert.NotNull(inputCodec);
            Assert.Equal(SKEncodedOrigin.RightTop, inputCodec.EncodedOrigin);
        }

        var result = await new SkiaSharpCompressionEngine().CompressAsync(
            CreateTask("jpg", "jpg", 80),
            inputPath,
            outputPath,
            _ => { },
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        using var outputCodec = SKCodec.Create(outputPath);
        Assert.NotNull(outputCodec);
        Assert.Equal(7, outputCodec.Info.Width);
        Assert.Equal(11, outputCodec.Info.Height);
        Assert.Equal(SKEncodedOrigin.TopLeft, outputCodec.EncodedOrigin);
    }

    private static void WriteImage(
        string path,
        SKEncodedImageFormat format,
        int width,
        int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 100);
        Assert.NotNull(data);
        using var output = File.Create(path);
        data.SaveTo(output);
    }

    private static void WriteJpegWithOrientation(string path, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        Assert.NotNull(data);
        var jpeg = data.ToArray();

        // EXIF orientation 6 (RightTop) means rotate the encoded pixels 90 degrees clockwise.
        byte[] orientationSegment =
        [
            0xff, 0xe1, 0x00, 0x22,
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
            0x49, 0x49, 0x2a, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,
            0x06, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];

        using var output = File.Create(path);
        output.Write(jpeg, 0, 2);
        output.Write(orientationSegment);
        output.Write(jpeg, 2, jpeg.Length - 2);
    }

    private static void WritePatternPng(string path, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SKColor(
                    (byte)(x * 17),
                    (byte)(y * 23),
                    (byte)((x + y) * 11),
                    255));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        Assert.NotNull(data);
        using var output = File.Create(path);
        data.SaveTo(output);
    }

    private static CompressionTask CreateTask(
        string originalFormat,
        string outputFormat,
        int quality) => new()
        {
            JobId = Guid.NewGuid(),
            Job = new Job(),
            Quality = quality,
            OriginalFileName = $"source.{originalFormat}",
            OriginalFormat = originalFormat,
            OutputFormat = outputFormat,
            OriginalSizeBytes = 1,
            DownloadUrl = "/api/v1/image-compression/file/test",
        };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "utilities-image-compression-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
