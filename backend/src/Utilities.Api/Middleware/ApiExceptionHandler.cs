using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Utilities.Api.Models.Common;
using Utilities.Application.Exceptions;

namespace Utilities.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted) return false;

        var (statusCode, message) = exception switch
        {
            RequestValidationException validation =>
                (StatusCodes.Status400BadRequest, validation.Message),
            ResourceNotFoundException notFound =>
                (StatusCodes.Status404NotFound, notFound.Message),
            ResourceConflictException conflict =>
                (StatusCodes.Status409Conflict, conflict.Message),
            ExternalServiceException { Failure: ExternalServiceFailure.InvalidResponse } external =>
                (StatusCodes.Status502BadGateway, external.Message),
            ExternalServiceException external =>
                (StatusCodes.Status500InternalServerError, external.Message),
            BadHttpRequestException badRequest =>
                (badRequest.StatusCode, badRequest.Message),
            JsonException =>
                (StatusCodes.Status400BadRequest, "The request body is invalid."),
            _ =>
                (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Request failed with status code {StatusCode}", statusCode);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse(statusCode, message),
            cancellationToken);
        return true;
    }
}
