namespace Utilities.Application.DTOs.Responses;

public sealed record JobState(
    double Progress,
    bool IsCompleted,
    bool IsFailed,
    string? Error);
