using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Utilities.Infrastructure.Configuration;

namespace Utilities.Api.Filters;

public sealed class FileConversionUploadLimitsFilter(
    IOptions<FileConversionOptions> options,
    ILogger<FileConversionUploadLimitsFilter> logger) :
    IAuthorizationFilter,
    IOrderedFilter,
    IRequestSizePolicy,
    IRequestFormLimitsPolicy
{
    private readonly FileConversionOptions _options = options.Value;

    public int Order => 900;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsEffectivePolicy<IRequestSizePolicy>(this))
        {
            ApplyRequestBodyLimit(context.HttpContext);
        }

        if (context.IsEffectivePolicy<IRequestFormLimitsPolicy>(this))
        {
            ApplyMultipartLimit(context.HttpContext);
        }
    }

    private void ApplyRequestBodyLimit(HttpContext httpContext)
    {
        var feature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is null)
        {
            logger.LogDebug(
                "The server does not expose a configurable request body size feature");
            return;
        }

        if (feature.IsReadOnly)
        {
            logger.LogWarning(
                "The request body size limit is already read-only and could not be set to {MaximumRequestBytes} bytes",
                _options.MaxUploadRequestBytes);
            return;
        }

        feature.MaxRequestBodySize = _options.MaxUploadRequestBytes;
    }

    private void ApplyMultipartLimit(HttpContext httpContext)
    {
        var formFeature = httpContext.Features.Get<IFormFeature>();
        if (formFeature?.Form is not null)
        {
            logger.LogWarning(
                "The request form was read before the file-conversion multipart limit could be applied");
            return;
        }

        httpContext.Features.Set<IFormFeature>(new FormFeature(
            httpContext.Request,
            new FormOptions
            {
                MultipartBodyLengthLimit = _options.MaxUploadFileBytes,
            }));
    }
}
