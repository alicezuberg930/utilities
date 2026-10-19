using Microsoft.Extensions.Options;

namespace Utilities.Infrastructure.Configuration;

public sealed class FileConversionOptionsValidator : IValidateOptions<FileConversionOptions>
{
    public ValidateOptionsResult Validate(string? name, FileConversionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.CadConverterExecutablePath))
        {
            failures.Add("FileConversion:CadConverterExecutablePath is required.");
        }

        if (options.CadConversionTimeoutSeconds is < 1 or > 7_200)
        {
            failures.Add("FileConversion:CadConversionTimeoutSeconds must be between 1 and 7200.");
        }

        if (!double.IsFinite(options.CadLinearDeflectionMeters) || options.CadLinearDeflectionMeters <= 0)
        {
            failures.Add("FileConversion:CadLinearDeflectionMeters must be a finite positive number.");
        }

        if (!double.IsFinite(options.CadAngularDeflectionDegrees) || options.CadAngularDeflectionDegrees <= 0 || options.CadAngularDeflectionDegrees > 180)
        {
            failures.Add("FileConversion:CadAngularDeflectionDegrees must be greater than 0 and at most 180.");
        }

        if (options.CadMaxInputBytes <= 0)
        {
            failures.Add("FileConversion:CadMaxInputBytes must be a positive number.");
        }

        ValidateCadCount(failures, nameof(options.CadMaxEntities), options.CadMaxEntities);
        ValidateCadCount(failures, nameof(options.CadMaxVertices), options.CadMaxVertices);
        ValidateCadCount(failures, nameof(options.CadMaxTriangles), options.CadMaxTriangles);
        ValidateCadCount(failures, nameof(options.CadMaxLineSegments), options.CadMaxLineSegments);

        if (options.CadMaxOutputBytes is <= 0 or > uint.MaxValue)
        {
            failures.Add($"FileConversion:CadMaxOutputBytes must be between 1 and {uint.MaxValue}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateCadCount(List<string> failures, string optionName, long value)
    {
        if (value is <= 0 or > uint.MaxValue)
        {
            failures.Add(
                $"FileConversion:{optionName} must be between 1 and {uint.MaxValue}.");
        }
    }
}
