using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Utilities.Api.Models.Common;

namespace Utilities.Api.Filters;

public sealed class ResponseEnvelopeFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var metadata = context.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<SkipResponseEnvelopeAttribute>().Any() || context.Result is not ObjectResult objectResult || objectResult.Value is ApiErrorResponse)
        {
            await next();
            return;
        }

        var envelope = metadata.OfType<ResponseEnvelopeAttribute>().LastOrDefault();
        var statusCode = envelope?.StatusCode ?? objectResult.StatusCode ?? StatusCodes.Status200OK;

        context.Result = new ObjectResult(new ApiResponse<object?>(statusCode, envelope?.Message, objectResult.Value))
        {
            StatusCode = statusCode,
        };

        await next();
    }
}
