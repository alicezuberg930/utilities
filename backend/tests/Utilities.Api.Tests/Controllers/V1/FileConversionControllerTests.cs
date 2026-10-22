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

public sealed class FileConversionControllerTests
{
    [Theory]
    [InlineData("report.pdf", 61)]
    [InlineData("drawing.dwg", 81)]
    [InlineData("movie.mp4", 101)]
    public async Task CreateJobAsync_FileAboveApplicableLimit_RejectsBeforeStorage(
        string fileName,
        long fileLength)
    {
        var service = new FakeFileConversion();
        var controller = CreateController(service);
        var file = new FormFile(Stream.Null, 0, fileLength, "file", fileName);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            controller.CreateJobAsync(file, "docx", CancellationToken.None));

        Assert.Contains("upload size limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public async Task CreateJobAsync_PdfAtConfiguredLimit_DelegatesToConversionService()
    {
        var service = new FakeFileConversion();
        var controller = CreateController(service);
        var content = new MemoryStream(new byte[60]);
        var file = new FormFile(content, 0, content.Length, "file", "report.pdf");

        await controller.CreateJobAsync(file, "docx", CancellationToken.None);

        Assert.True(service.CreateCalled);
        Assert.NotNull(service.Request);
        Assert.Equal(60, service.Request.Length);
        Assert.Equal("report.pdf", service.Request.FileName);
    }

    private static FileConversionController CreateController(FakeFileConversion service) =>
        new(
            service,
            new UnusedProgressStream(),
            Options.Create(new FileConversionOptions
            {
                PdfDocxMaxInputBytes = 60,
                CadMaxInputBytes = 80,
                MaxUploadFileBytes = 100,
                MaxUploadRequestBytes = 100 + FileConversionOptions.MinimumMultipartOverheadBytes,
            }));

    private sealed class FakeFileConversion : IFileConversion
    {
        public bool CreateCalled { get; private set; }
        public CreateFileConversionRequest? Request { get; private set; }

        public Task<FileConversionJobResponse> CreateJobAsync(
            CreateFileConversionRequest request,
            CancellationToken cancellationToken)
        {
            CreateCalled = true;
            Request = request;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new FileConversionJobResponse(
                Guid.NewGuid(),
                "pending",
                "pdf",
                "docx",
                "/api/v1/convert/file/test",
                0,
                now,
                now));
        }

        public Task<JobState?> GetJobStateAsync(
            Guid jobId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<JobDownloadFile> GetDownloadFileAsync(
            Guid jobId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedProgressStream : IFileProgressStream
    {
        public IJobProgressSubscription Subscribe(Guid jobId) =>
            throw new NotSupportedException();
    }
}
