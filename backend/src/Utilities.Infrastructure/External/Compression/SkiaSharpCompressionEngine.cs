using SkiaSharp;
using Utilities.Application.Interfaces;
using Utilities.Domain.Entities;

namespace Utilities.Infrastructure.External.Compression;

public sealed class SkiaSharpCompressionEngine : IImageCompressionEngine
{
    private const long MaximumPixels = 100_000_000;

    public async Task<ImageCompressionResult> CompressAsync(
        CompressionTask task,
        string inputPath,
        string outputPath,
        Action<double> reportProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            reportProgress(10);

            using var codec = SKCodec.Create(inputPath);
            if (codec is null)
            {
                return ImageCompressionResult.Failed(
                    "The image could not be decoded or compressed.");
            }

            if (!MatchesExpectedFormat(task.OriginalFormat, codec.EncodedFormat))
            {
                return ImageCompressionResult.Failed(
                    "The image content does not match its file extension.");
            }

            var pixelCount = checked((long)codec.Info.Width * codec.Info.Height);
            if (pixelCount > MaximumPixels)
            {
                return ImageCompressionResult.Failed(
                    "The image dimensions are too large to process safely.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            reportProgress(25);

            using var decodedBitmap = SKBitmap.Decode(codec);
            if (decodedBitmap is null)
            {
                return ImageCompressionResult.Failed(
                    "The image could not be decoded or compressed.");
            }

            using var orientedBitmap = OrientBitmap(decodedBitmap, codec.EncodedOrigin);

            cancellationToken.ThrowIfCancellationRequested();
            reportProgress(55);

            using var encodedData = Encode(
                orientedBitmap ?? decodedBitmap,
                task.OutputFormat,
                task.Quality);
            if (encodedData is null)
            {
                return ImageCompressionResult.Failed(
                    "The image could not be decoded or compressed.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            await using (var output = new FileStream(
                             outputPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                encodedData.SaveTo(output);
                await output.FlushAsync(cancellationToken);
            }

            reportProgress(90);
            return ImageCompressionResult.Succeeded(new FileInfo(outputPath).Length);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            TryDeleteOutput(outputPath);
            return ImageCompressionResult.Failed(
                "The image could not be decoded or compressed.");
        }
    }

    private static SKBitmap? OrientBitmap(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return null;
        }

        var swapsDimensions = origin is
            SKEncodedOrigin.LeftTop or
            SKEncodedOrigin.RightTop or
            SKEncodedOrigin.RightBottom or
            SKEncodedOrigin.LeftBottom;
        var destination = new SKBitmap(
            swapsDimensions ? source.Height : source.Width,
            swapsDimensions ? source.Width : source.Height,
            source.ColorType,
            source.AlphaType);

        using var canvas = new SKCanvas(destination);
        canvas.SetMatrix(CreateOrientationMatrix(origin, source.Width, source.Height));
        canvas.DrawBitmap(source, 0, 0, SKSamplingOptions.Default, paint: null);
        canvas.Flush();
        return destination;
    }

    private static SKData? Encode(SKBitmap bitmap, string outputFormat, int quality)
    {
        if (outputFormat == "png")
        {
            using var pixmap = bitmap.PeekPixels();
            return pixmap?.Encode(new SKPngEncoderOptions(
                SKPngEncoderFilterFlags.AllFilters,
                GetPngCompressionLevel(quality)));
        }

        using var image = SKImage.FromBitmap(bitmap);
        var encodedFormat = outputFormat == "jpg"
            ? SKEncodedImageFormat.Jpeg
            : SKEncodedImageFormat.Webp;
        return image.Encode(encodedFormat, quality);
    }

    private static int GetPngCompressionLevel(int compressionEffort) =>
        Math.Clamp((int)Math.Ceiling(compressionEffort * 9d / 100d), 1, 9);

    private static SKMatrix CreateOrientationMatrix(
        SKEncodedOrigin origin,
        int width,
        int height) => origin switch
        {
            SKEncodedOrigin.TopRight => CreateMatrix(-1, 0, width, 0, 1, 0),
            SKEncodedOrigin.BottomRight => CreateMatrix(-1, 0, width, 0, -1, height),
            SKEncodedOrigin.BottomLeft => CreateMatrix(1, 0, 0, 0, -1, height),
            SKEncodedOrigin.LeftTop => CreateMatrix(0, 1, 0, 1, 0, 0),
            SKEncodedOrigin.RightTop => CreateMatrix(0, -1, height, 1, 0, 0),
            SKEncodedOrigin.RightBottom => CreateMatrix(0, -1, height, -1, 0, width),
            SKEncodedOrigin.LeftBottom => CreateMatrix(0, 1, 0, -1, 0, width),
            _ => SKMatrix.CreateIdentity(),
        };

    private static SKMatrix CreateMatrix(
        float scaleX,
        float skewX,
        float transX,
        float skewY,
        float scaleY,
        float transY) => new()
        {
            ScaleX = scaleX,
            SkewX = skewX,
            TransX = transX,
            SkewY = skewY,
            ScaleY = scaleY,
            TransY = transY,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1,
        };

    private static bool MatchesExpectedFormat(
        string originalFormat,
        SKEncodedImageFormat decodedFormat) => originalFormat switch
        {
            "jpeg" or "jpg" => decodedFormat == SKEncodedImageFormat.Jpeg,
            "png" => decodedFormat == SKEncodedImageFormat.Png,
            "webp" => decodedFormat == SKEncodedImageFormat.Webp,
            _ => false,
        };

    private static void TryDeleteOutput(string outputPath)
    {
        try
        {
            File.Delete(outputPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
