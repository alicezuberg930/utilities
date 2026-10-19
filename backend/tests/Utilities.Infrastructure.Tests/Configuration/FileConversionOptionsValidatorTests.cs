using Utilities.Infrastructure.Configuration;
using Xunit;

namespace Utilities.Infrastructure.Tests.Configuration;

public sealed class FileConversionOptionsValidatorTests
{
    private readonly FileConversionOptionsValidator _validator = new();

    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        var options = new FileConversionOptions();
        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
        Assert.Equal(256L * 1024 * 1024, options.CadMaxInputBytes);
        Assert.Equal(250_000, options.CadMaxEntities);
        Assert.Equal(5_000_000, options.CadMaxVertices);
        Assert.Equal(2_000_000, options.CadMaxTriangles);
        Assert.Equal(2_000_000, options.CadMaxLineSegments);
        Assert.Equal(256L * 1024 * 1024, options.CadMaxOutputBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7201)]
    public void Validate_InvalidTimeout_Fails(int value)
    {
        var options = new FileConversionOptions
        {
            CadConversionTimeoutSeconds = value,
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("CadConversionTimeoutSeconds", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_InvalidLinearDeflection_Fails(double value)
    {
        var options = new FileConversionOptions
        {
            CadLinearDeflectionMeters = value,
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("CadLinearDeflectionMeters", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(180.1)]
    [InlineData(double.NaN)]
    public void Validate_InvalidAngularDeflection_Fails(double value)
    {
        var options = new FileConversionOptions
        {
            CadAngularDeflectionDegrees = value,
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("CadAngularDeflectionDegrees", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_MissingExecutablePath_Fails()
    {
        var options = new FileConversionOptions
        {
            CadConverterExecutablePath = " ",
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("CadConverterExecutablePath", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(FileConversionOptions.CadMaxInputBytes), 0L)]
    [InlineData(nameof(FileConversionOptions.CadMaxInputBytes), -1L)]
    [InlineData(nameof(FileConversionOptions.CadMaxEntities), 0L)]
    [InlineData(nameof(FileConversionOptions.CadMaxEntities), -1L)]
    [InlineData(nameof(FileConversionOptions.CadMaxVertices), 0L)]
    [InlineData(nameof(FileConversionOptions.CadMaxVertices), -1L)]
    [InlineData(nameof(FileConversionOptions.CadMaxTriangles), 0L)]
    [InlineData(nameof(FileConversionOptions.CadMaxTriangles), -1L)]
    [InlineData(nameof(FileConversionOptions.CadMaxLineSegments), 0L)]
    [InlineData(nameof(FileConversionOptions.CadMaxLineSegments), -1L)]
    [InlineData(nameof(FileConversionOptions.CadMaxOutputBytes), 0L)]
    [InlineData(nameof(FileConversionOptions.CadMaxOutputBytes), -1L)]
    public void Validate_NonPositiveCadResourceLimit_Fails(string propertyName, long value)
    {
        var options = new FileConversionOptions();
        SetCadResourceLimit(options, propertyName, value);

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(propertyName, result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(FileConversionOptions.CadMaxEntities))]
    [InlineData(nameof(FileConversionOptions.CadMaxVertices))]
    [InlineData(nameof(FileConversionOptions.CadMaxTriangles))]
    [InlineData(nameof(FileConversionOptions.CadMaxLineSegments))]
    [InlineData(nameof(FileConversionOptions.CadMaxOutputBytes))]
    public void Validate_GlbBoundCadResourceLimitAboveUIntMaximum_Fails(string propertyName)
    {
        var options = new FileConversionOptions();
        SetCadResourceLimit(options, propertyName, (long)uint.MaxValue + 1);

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(propertyName, result.FailureMessage, StringComparison.Ordinal);
    }

    private static void SetCadResourceLimit(
        FileConversionOptions options,
        string propertyName,
        long value)
    {
        switch (propertyName)
        {
            case nameof(FileConversionOptions.CadMaxInputBytes):
                options.CadMaxInputBytes = value;
                break;
            case nameof(FileConversionOptions.CadMaxEntities):
                options.CadMaxEntities = value;
                break;
            case nameof(FileConversionOptions.CadMaxVertices):
                options.CadMaxVertices = value;
                break;
            case nameof(FileConversionOptions.CadMaxTriangles):
                options.CadMaxTriangles = value;
                break;
            case nameof(FileConversionOptions.CadMaxLineSegments):
                options.CadMaxLineSegments = value;
                break;
            case nameof(FileConversionOptions.CadMaxOutputBytes):
                options.CadMaxOutputBytes = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(propertyName));
        }
    }
}
