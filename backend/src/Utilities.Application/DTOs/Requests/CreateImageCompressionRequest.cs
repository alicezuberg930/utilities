namespace Utilities.Application.DTOs.Requests;

public sealed record CreateImageCompressionRequest(
    string FileName,
    long Length,
    int Quality,
    bool ConvertToWebp,
    Stream Content);
