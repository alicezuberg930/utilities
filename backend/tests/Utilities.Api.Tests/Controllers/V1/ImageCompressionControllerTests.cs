using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Utilities.Api.Controllers.V1;
using Utilities.Application.DTOs.Requests;
using Utilities.Application.DTOs.Responses;
using Utilities.Application.Exceptions;
using Utilities.Application.Interfaces;
using Utilities.Infrastructure.Configuration;
using Xunit;

namespace Utilities.Api.Tests.Controllers.V1;

public sealed class ImageCompressionControllerTests
{
    [Fact]
    public async Task CreateJobAsync_ValidImage_DelegatesQualityAndFileToService()
    {
        var service = new FakeImageCompression();
        var controller = CreateController(service);
        var content = new MemoryStream([1, 2, 3]);
        var file = new FormFile(content, 0, content.Length, "file", "photo.png");

        await controller.CreateJobAsync(file, 72, true, CancellationToken.None);

        Assert.NotNull(service.Request);
        Assert.Equal("photo.png", service.Request.FileName);
        Assert.Equal(72, service.Request.Quality);
        Assert.True(service.Request.ConvertToWebp);
        Assert.Equal(content.Length, service.Request.Length);
    }

    [Fact]
    public async Task CreateJobAsync_ImageAboveLimit_RejectsBeforeService()
    {
        var service = new FakeImageCompression();
        var controller = CreateController(service, maxUploadBytes: 10);
        var file = new FormFile(Stream.Null, 0, 11, "file", "photo.jpg");

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            controller.CreateJobAsync(file, 80, false, CancellationToken.None));

        Assert.Null(service.Request);
    }

    private static ImageCompressionController CreateController(
        FakeImageCompression service,
        long maxUploadBytes = 20L * 1024 * 1024) =>
        new(
            service,
            new UnusedProgressStream(),
            Options.Create(new FileConversionOptions
            {
                MaxUploadFileBytes = maxUploadBytes,
                MaxUploadRequestBytes = maxUploadBytes +
                    FileConversionOptions.MinimumMultipartOverheadBytes,
            }));

    private sealed class FakeImageCompression : IImageCompression
    {
        public CreateImageCompressionRequest? Request { get; private set; }

        public Task<ImageCompressionJobResponse> CreateJobAsync(
            CreateImageCompressionRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new ImageCompressionJobResponse(
                Guid.NewGuid(),
                "pending",
                request.Quality,
                request.FileName,
                "png",
                "webp",
                request.Length,
                null,
                "/api/v1/image-compression/file/test",
                0,
                now,
                now));
        }

        public Task<JobState?> GetJobStateAsync(
            Guid jobId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<JobDownloadFile> GetDownloadFileAsync(
            Guid jobId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class UnusedProgressStream : IImageCompressionProgressStream
    {
        public IJobProgressSubscription Subscribe(Guid jobId) =>
            throw new NotSupportedException();
    }
}
