namespace Utilities.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data: https:; " +
        "connect-src 'self' https:; " +
        "frame-src 'self' https:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "upgrade-insecure-requests";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var response = (HttpResponse)state;
            response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;
            response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            response.Headers.XContentTypeOptions = "nosniff";
            return Task.CompletedTask;
        }, context.Response);

        await next(context);
    }
}
