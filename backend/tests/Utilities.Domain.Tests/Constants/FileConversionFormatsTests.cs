using Utilities.Domain.Constants;
using Xunit;

namespace Utilities.Domain.Tests.Constants;

public sealed class FileConversionFormatsTests
{
    [Theory]
    [InlineData("mp3", "wav", true)]
    [InlineData("mp4", "mp3", true)]
    [InlineData("mp4", "webm", true)]
    [InlineData("dwg", "glb", true)]
    [InlineData("dxf", "glb", true)]
    [InlineData("png", "webp", true)]
    [InlineData("psd", "png", true)]
    [InlineData("svg", "avif", true)]
    [InlineData("mp3", "mp3", false)]
    [InlineData("mp3", "mp4", false)]
    [InlineData("png", "png", false)]
    [InlineData("png", "mp4", false)]
    [InlineData("mp4", "png", false)]
    [InlineData("dwg", "mp4", false)]
    [InlineData("DWG", "glb", false)]
    public void IsSupportedConversion_Formats_ReturnsExpectedResult(
        string originalFormat,
        string convertedFormat,
        bool expected)
    {
        var result = FileConversionFormats.IsSupportedConversion(originalFormat, convertedFormat);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ImageOutputs_AllBelongToImageInputs()
    {
        Assert.All(
            FileConversionFormats.ImageOutputs,
            format => Assert.Contains(format, FileConversionFormats.ImageInputs));
    }
}
