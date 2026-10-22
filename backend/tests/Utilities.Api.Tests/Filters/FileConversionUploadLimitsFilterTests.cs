using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Utilities.Api.Filters;
using Utilities.Infrastructure.Configuration;
using Xunit;

namespace Utilities.Api.Tests.Filters;

public sealed class FileConversionUploadLimitsFilterTests
{
    [Fact]
    public void OnAuthorization_AppliesConfiguredRequestBodyLimit()
    {
        var options = CreateOptions();
        var filter = CreateFilter(options);
        var httpContext = new DefaultHttpContext();
        var bodySizeFeature = new TestMaxRequestBodySizeFeature();
        httpContext.Features.Set<IHttpMaxRequestBodySizeFeature>(bodySizeFeature);

        filter.OnAuthorization(CreateContext(httpContext, filter));

        Assert.Equal(options.MaxUploadRequestBytes, bodySizeFeature.MaxRequestBodySize);
    }

    [Fact]
    public async Task OnAuthorization_AppliesConfiguredMultipartFileLimit()
    {
        const string boundary = "test-boundary";
        var options = CreateOptions(maxUploadFileBytes: 8);
        var filter = CreateFilter(options);
        var httpContext = new DefaultHttpContext();
        var body = string.Join(
            "\r\n",
            $"--{boundary}",
            "Content-Disposition: form-data; name=\"file\"; filename=\"source.pdf\"",
            "Content-Type: application/pdf",
            string.Empty,
            "123456789",
            $"--{boundary}--",
            string.Empty);
        httpContext.Request.ContentType = $"multipart/form-data; boundary={boundary}";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        filter.OnAuthorization(CreateContext(httpContext, filter));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            httpContext.Request.ReadFormAsync());
        Assert.Contains(
            "Multipart body length limit 8 exceeded",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OnAuthorization_ReadOnlyServerLimit_IsNotOverwritten()
    {
        var filter = CreateFilter(CreateOptions());
        var httpContext = new DefaultHttpContext();
        var bodySizeFeature = new TestMaxRequestBodySizeFeature
        {
            IsReadOnly = true,
            MaxRequestBodySize = 123,
        };
        httpContext.Features.Set<IHttpMaxRequestBodySizeFeature>(bodySizeFeature);

        filter.OnAuthorization(CreateContext(httpContext, filter));

        Assert.Equal(123, bodySizeFeature.MaxRequestBodySize);
    }

    private static FileConversionOptions CreateOptions(long maxUploadFileBytes = 1024) =>
        new()
        {
            MaxUploadFileBytes = maxUploadFileBytes,
            MaxUploadRequestBytes = maxUploadFileBytes + FileConversionOptions.MinimumMultipartOverheadBytes,
        };

    private static FileConversionUploadLimitsFilter CreateFilter(FileConversionOptions options) =>
        new(
            Options.Create(options),
            NullLogger<FileConversionUploadLimitsFilter>.Instance);

    private static AuthorizationFilterContext CreateContext(
        HttpContext httpContext,
        FileConversionUploadLimitsFilter filter) =>
        new(
            new ActionContext(
                httpContext,
                new RouteData(),
                new ActionDescriptor()),
            [filter]);

    private sealed class TestMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly { get; init; }
        public long? MaxRequestBodySize { get; set; }
    }
}
