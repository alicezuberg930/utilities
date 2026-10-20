using Utilities.Application.DTOs.Requests;
using Utilities.Application.Exceptions;
using Utilities.Application.Interfaces;
using Utilities.Application.Services;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Xunit;

namespace Utilities.Application.Tests.Services;

public sealed class FileConversionServiceTests
{
    [Fact]
    public async Task CreateJobAsync_ValidCadRequest_NormalizesPersistsAndQueuesJob()
    {
        var calls = new List<string>();
        var repository = new FakeConvertTaskRepository(calls);
        var queue = new FakeFileConversionQueue(calls);
        var storage = new FakeFileConversionStorage(calls);
        var service = new FileConversionService(repository, queue, storage);
        await using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        var response = await service.CreateJobAsync(
            new CreateFileConversionRequest("house.DWG", content.Length, ".GLB", content),
            CancellationToken.None);

        Assert.NotNull(repository.AddedTask);
        Assert.Equal(response.JobId, repository.AddedTask.JobId);
        Assert.Equal(response.JobId, storage.SavedJobId);
        Assert.Equal(response.JobId, queue.QueuedJobId);
        Assert.Equal("dwg", repository.AddedTask.OriginalFormat);
        Assert.Equal("glb", repository.AddedTask.ConvertedFormat);
        Assert.Equal(JobStatuses.Pending, repository.AddedTask.Status);
        Assert.Equal("dwg", storage.SavedFormat);
        Assert.Equal(["ensure", "save", "add", "queue"], calls);
    }

    [Fact]
    public async Task CreateJobAsync_EmptySource_ThrowsBeforeCreatingSideEffects()
    {
        var calls = new List<string>();
        var service = new FileConversionService(
            new FakeConvertTaskRepository(calls),
            new FakeFileConversionQueue(calls),
            new FakeFileConversionStorage(calls));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.CreateJobAsync(
                new CreateFileConversionRequest("source.mp4", 0, "mp3", Stream.Null),
                CancellationToken.None));

        Assert.Equal("The source file cannot be empty.", exception.Message);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task CreateJobAsync_ValidImageRequest_NormalizesPersistsAndQueuesJob()
    {
        var calls = new List<string>();
        var repository = new FakeConvertTaskRepository(calls);
        var queue = new FakeFileConversionQueue(calls);
        var storage = new FakeFileConversionStorage(calls);
        var service = new FileConversionService(repository, queue, storage);
        await using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        var response = await service.CreateJobAsync(
            new CreateFileConversionRequest("design.PSD", content.Length, ".WEBP", content),
            CancellationToken.None);

        Assert.NotNull(repository.AddedTask);
        Assert.Equal(response.JobId, repository.AddedTask.JobId);
        Assert.Equal("psd", repository.AddedTask.OriginalFormat);
        Assert.Equal("webp", repository.AddedTask.ConvertedFormat);
        Assert.Equal("psd", storage.SavedFormat);
        Assert.Equal(["ensure", "save", "add", "queue"], calls);
    }

    [Fact]
    public async Task GetDownloadFileAsync_CompletedAvifJob_ReturnsImageContentType()
    {
        var calls = new List<string>();
        var jobId = Guid.NewGuid();
        var task = new ConvertTask
        {
            JobId = jobId,
            Job = new Job { JobId = jobId },
            Status = JobStatuses.Completed,
            OriginalFormat = "png",
            ConvertedFormat = "avif",
            DownloadUrl = $"/api/v1/convert/file/{jobId}",
        };
        var service = new FileConversionService(
            new FakeConvertTaskRepository(calls, task),
            new FakeFileConversionQueue(calls),
            new FakeFileConversionStorage(calls) { OutputExists = true });

        var file = await service.GetDownloadFileAsync(jobId, CancellationToken.None);

        Assert.Equal("image/avif", file.ContentType);
        Assert.Equal($"{jobId:N}.avif", file.FileName);
    }

    private sealed class FakeConvertTaskRepository(
        List<string> calls,
        ConvertTask? foundTask = null) : IConvertTaskRepository
    {
        public ConvertTask? AddedTask { get; private set; }

        public Task AddAsync(ConvertTask task, CancellationToken cancellationToken)
        {
            calls.Add("add");
            AddedTask = task;
            return Task.CompletedTask;
        }

        public Task<ConvertTask?> FindAsync(Guid jobId, CancellationToken cancellationToken) =>
            Task.FromResult(foundTask);

        public Task<IReadOnlyList<Guid>> FindInterruptedJobIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeFileConversionQueue(List<string> calls) : IFileConversionQueue
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

    private sealed class FakeFileConversionStorage(List<string> calls) : IFileConversionStorage
    {
        public Guid? SavedJobId { get; private set; }
        public string? SavedFormat { get; private set; }
        public bool OutputExists { get; init; }

        public string GetInputPath(Guid jobId, string originalFormat) =>
            Path.Combine("input", $"{jobId:N}.{originalFormat}");

        public string GetOutputPath(ConvertTask task) =>
            Path.Combine("output", $"{task.JobId:N}.{task.ConvertedFormat}");

        public Task SaveInputAsync(
            Guid jobId,
            string originalFormat,
            Stream content,
            CancellationToken cancellationToken)
        {
            calls.Add("save");
            SavedJobId = jobId;
            SavedFormat = originalFormat;
            return Task.CompletedTask;
        }

        public bool Exists(string path) => OutputExists;

        public void DeleteIfExists(string path)
        {
        }

        public void EnsureDirectoryExists() => calls.Add("ensure");
    }
}
