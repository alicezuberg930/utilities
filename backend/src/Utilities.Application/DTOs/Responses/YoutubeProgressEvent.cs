namespace Utilities.Application.DTOs.Responses;

public sealed record YoutubeProgress(double Progress);

public sealed record YoutubeFailure(string Message);

public sealed record YoutubeProgressEvent(string? Name, object Data);
