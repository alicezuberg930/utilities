namespace Utilities.Application.DTOs.Requests;

public sealed record CreateFileConversionRequest(
    string FileName,
    long Length,
    string? ConvertedFormat,
    Stream Content);
