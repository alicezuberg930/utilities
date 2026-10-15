namespace Utilities.Application.DTOs.Responses;

public sealed record JobProgress(double Progress);

public sealed record JobFailure(string Message);

public sealed record JobProgressEvent(string? Name, object Data);
