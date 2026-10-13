namespace Utilities.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ResponseEnvelopeAttribute(int statusCode = StatusCodes.Status200OK, string? message = null) : Attribute
{
    public int StatusCode { get; } = statusCode;
    public string? Message { get; } = message;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipResponseEnvelopeAttribute : Attribute;
