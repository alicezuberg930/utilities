namespace Utilities.Application.Exceptions;

public sealed class ResourceConflictException(string message) : Exception(message);
