using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Utilities.Api.Middleware;
using Utilities.Api.Models.Common;
using Utilities.Application.Exceptions;
using Xunit;

namespace Utilities.Api.Tests.Middleware;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_RequestValidationException_WritesBadRequestEnvelope()
    {
        var (handled, context, response) = await HandleAsync(
            new RequestValidationException("The request is invalid."));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("The request is invalid.", response.Message);
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_HidesInternalDetails()
    {
        var (handled, context, response) = await HandleAsync(
            new InvalidOperationException("sensitive details"));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status500InternalServerError, response.StatusCode);
        Assert.Equal("An unexpected error occurred.", response.Message);
    }

    private static async Task<(bool Handled, DefaultHttpContext Context, ApiErrorResponse Response)>
        HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        var response = await JsonSerializer.DeserializeAsync<ApiErrorResponse>(
            context.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return (handled, context, Assert.IsType<ApiErrorResponse>(response));
    }
}
