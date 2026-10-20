using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Utilities.Infrastructure.Configuration;
using Utilities.Infrastructure.External.Conversion;
using Xunit;

namespace Utilities.Infrastructure.Tests.External.Conversion;

public sealed class MediaFileConversionEngineTests
{
    private readonly MediaFileConversionEngine _engine = new(
        Options.Create(new FileConversionOptions()),
        NullLogger<MediaFileConversionEngine>.Instance);

    [Theory]
    [InlineData("png", "webp", true)]
    [InlineData("psd", "png", true)]
    [InlineData("svg", "avif", true)]
    [InlineData("png", "png", false)]
    [InlineData("png", "mp4", false)]
    [InlineData("mp4", "png", false)]
    public void CanConvert_Formats_ReturnsExpectedResult(
        string originalFormat,
        string convertedFormat,
        bool expected)
    {
        Assert.Equal(expected, _engine.CanConvert(originalFormat, convertedFormat));
    }
}
