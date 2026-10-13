namespace Utilities.Application.Exceptions;

public enum ExternalServiceFailure
{
    Unavailable,
    InvalidResponse,
}

public sealed class ExternalServiceException(
    string message,
    ExternalServiceFailure failure,
    Exception? innerException = null) : Exception(message, innerException)
{
    public ExternalServiceFailure Failure { get; } = failure;
}
