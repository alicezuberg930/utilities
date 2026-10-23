using Utilities.Application.DTOs.Requests;
using Utilities.Application.Exceptions;
using Utilities.Application.Interfaces;
using Utilities.Application.Services;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Xunit;

namespace Utilities.Application.Tests.Services;

public sealed class ImageCompressionServiceTests
{
    [Theory]
    [InlineData("photo.JPG", false, "jpg", "jpg")]
    [InlineData("photo.jpeg", false, "jpeg", "jpg")]
    [InlineData("graphic.PNG", false, "png", "png")]
    [InlineData("graphic.webp", false, "webp", "webp")]
    [InlineData("photo.jpg", true, "jpg", "webp")]
    [InlineData("graphic.png", true, "png", "webp")]
    public async Task CreateJobAsync_ValidImage_PersistsAndQueuesJob(
        string fileName,
        bool convertToWebp,
        string expectedOriginalFormat,
        string expectedOutputFormat)
    {
        var calls = new List<string>();
        var repository = new FakeCompressionTaskRepository(calls);
        var queue = new FakeImageCompressionQueue(calls);
        var storage = new FakeImageCompressionStorage(calls);
        var service = new ImageCompressionService(repository, queue, storage);
        await using var content = new MemoryStream([1, 2, 3]);

        var response = await service.CreateJobAsync(
            new CreateImageCompressionRequest(
                fileName,
                content.Length,
                73,
                convertToWebp,
                content),
            CancellationToken.None);

        Assert.NotNull(repository.AddedTask);
        Assert.Equal(response.JobId, repository.AddedTask.JobId);
        Assert.Equal(response.JobId, queue.QueuedJobId);
        Assert.Equal(expectedOriginalFormat, repository.AddedTask.OriginalFormat);
        Assert.Equal(expectedOutputFormat, repository.AddedTask.OutputFormat);
        Assert.Equal(73, repository.AddedTask.Quality);
        Assert.Equal(content.Length, repository.AddedTask.OriginalSizeBytes);
        Assert.Equal(JobStatuses.Pending, repository.AddedTask.Status);
        Assert.Equal(["ensure", "save", "add", "queue"], calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task CreateJobAsync_QualityOutsideRange_ThrowsWithoutSideEffects(int quality)
    {
        var calls = new List<string>();
        var service = new ImageCompressionService(
            new FakeCompressionTaskRepository(calls),
            new FakeImageCompressionQueue(calls),
            new FakeImageCompressionStorage(calls));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.CreateJobAsync(
                new CreateImageCompressionRequest(
                    "photo.jpg",
                    1,
                    quality,
                    false,
                    new MemoryStream([1])),
                CancellationToken.None));

        Assert.Contains("between 1 and 100", exception.Message);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task CreateJobAsync_UnsupportedImage_ThrowsWithoutSideEffects()
    {
        var calls = new List<string>();
        var service = new ImageCompressionService(
            new FakeCompressionTaskRepository(calls),
            new FakeImageCompressionQueue(calls),
            new FakeImageCompressionStorage(calls));

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.CreateJobAsync(
                new CreateImageCompressionRequest(
                    "photo.gif",
                    1,
                    80,
                    false,
                    new MemoryStream([1])),
                CancellationToken.None));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task GetDownloadFileAsync_CompletedJob_ReturnsFriendlyFileNameAndType()
    {
        var calls = new List<string>();
        var jobId = Guid.NewGuid();
        var task = new CompressionTask
        {
            JobId = jobId,
            Job = new Job { JobId = jobId },
            Status = JobStatuses.Completed,
            Quality = 80,
            OriginalFileName = "holiday photo.png",
            OriginalFormat = "png",
            OutputFormat = "webp",
            OriginalSizeBytes = 2048,
            CompressedSizeBytes = 1024,
            DownloadUrl = $"/api/v1/image-compression/file/{jobId}",
        };
        var service = new ImageCompressionService(
            new FakeCompressionTaskRepository(calls, task),
            new FakeImageCompressionQueue(calls),
            new FakeImageCompressionStorage(calls) { OutputExists = true });

        var file = await service.GetDownloadFileAsync(jobId, CancellationToken.None);

        Assert.Equal("image/webp", file.ContentType);
        Assert.Equal("holiday photo-compressed.webp", file.FileName);
    }

    [Fact]
    public async Task GetDownloadFileAsync_PreservedPng_ReturnsPngFileNameAndType()
    {
        var calls = new List<string>();
        var jobId = Guid.NewGuid();
        var task = new CompressionTask
        {
            JobId = jobId,
            Job = new Job { JobId = jobId },
            Status = JobStatuses.Completed,
            Quality = 80,
            OriginalFileName = "diagram.png",
            OriginalFormat = "png",
            OutputFormat = "png",
            OriginalSizeBytes = 2048,
            CompressedSizeBytes = 1024,
            DownloadUrl = $"/api/v1/image-compression/file/{jobId}",
        };
        var service = new ImageCompressionService(
            new FakeCompressionTaskRepository(calls, task),
            new FakeImageCompressionQueue(calls),
            new FakeImageCompressionStorage(calls) { OutputExists = true });

        var file = await service.GetDownloadFileAsync(jobId, CancellationToken.None);

        Assert.Equal("image/png", file.ContentType);
        Assert.Equal("diagram-compressed.png", file.FileName);
    }

    private sealed class FakeCompressionTaskRepository(
        List<string> calls,
        CompressionTask? foundTask = null) : ICompressionTaskRepository
    {
        public CompressionTask? AddedTask { get; private set; }

        public Task AddAsync(CompressionTask task, CancellationToken cancellationToken)
        {
            calls.Add("add");
            AddedTask = task;
            return Task.CompletedTask;
        }

        public Task<CompressionTask?> FindAsync(Guid jobId, CancellationToken cancellationToken) =>
            Task.FromResult(foundTask);

        public Task<IReadOnlyList<Guid>> FindInterruptedJobIdsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeImageCompressionQueue(List<string> calls) : IImageCompressionQueue
    {
        public Guid? QueuedJobId { get; private set; }

        public ValueTask QueueAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            calls.Add("queue");
            QueuedJobId = jobId;
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeImageCompressionStorage(List<string> calls) : IImageCompressionStorage
    {
        public bool OutputExists { get; init; }

        public string GetInputPath(Guid jobId, string originalFormat) =>
            Path.Combine("input", $"{jobId:N}.{originalFormat}");

        public string GetOutputPath(CompressionTask task) =>
            Path.Combine("output", $"{task.JobId:N}.{task.OutputFormat}");

        public Task SaveInputAsync(
            Guid jobId,
            string originalFormat,
            Stream content,
            CancellationToken cancellationToken)
        {
            calls.Add("save");
            return Task.CompletedTask;
        }

        public bool Exists(string path) => OutputExists;
        public long GetFileSize(string path) => 0;
        public void DeleteIfExists(string path) { }
        public void EnsureDirectoryExists() => calls.Add("ensure");
    }
}
