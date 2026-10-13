namespace Utilities.Application.DTOs.Responses;

public sealed record YoutubeJobState(
    double Progress,
    bool IsCompleted,
    bool IsFailed,
    string? Error);
