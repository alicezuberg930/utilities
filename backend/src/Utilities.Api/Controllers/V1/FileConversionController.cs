using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Utilities.Api.Filters;
using Utilities.Application.DTOs.Requests;
using Utilities.Application.DTOs.Responses;
using Utilities.Application.Exceptions;
using Utilities.Application.Interfaces;

namespace Utilities.Api.Controllers.V1;

[ApiController]
[Route("api/v1/convert")]
public sealed class FileConversionController(
    IFileConversion fileConversion,
    IFileProgressStream progressStream) : ControllerBase
{
    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("job")]
    [Consumes("multipart/form-data")]
    [ResponseEnvelope(StatusCodes.Status201Created, "Conversion job created successfully")]
    public async Task<FileConversionJobResponse> CreateJobAsync(
        [FromForm] IFormFile? file,
        [FromForm] string? convertedFormat,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw new RequestValidationException("A source file is required.");
        }

        await using var content = file.OpenReadStream();
        return await fileConversion.CreateJobAsync(
            new CreateFileConversionRequest(
                file.FileName,
                file.Length,
                convertedFormat,
                content),
            cancellationToken);
    }

    [HttpGet("progress/{jobId:guid}")]
    [SkipResponseEnvelope]
    public async Task StreamProgressAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var subscription = progressStream.Subscribe(jobId);
        var job = await fileConversion.GetJobStateAsync(jobId, cancellationToken) ?? throw new ResourceNotFoundException("File conversion job was not found.");

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        await Response.StartAsync(cancellationToken);
        await WriteEventAsync(new JobProgressEvent(null, new JobProgress(job.Progress)), cancellationToken);

        if (job.IsCompleted)
        {
            await WriteEventAsync(new JobProgressEvent("complete", "done"), cancellationToken);
            return;
        }

        if (job.IsFailed)
        {
            await WriteEventAsync(
                new JobProgressEvent("error", new JobFailure(job.Error ?? "The conversion failed.")),
                cancellationToken);
            return;
        }

        try
        {
            await foreach (var progressEvent in subscription.ReadAllAsync(cancellationToken))
            {
                await WriteEventAsync(progressEvent, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The browser closed the EventSource connection.
        }
    }

    [HttpGet("file/{jobId:guid}")]
    [SkipResponseEnvelope]
    public async Task<IActionResult> DownloadFileAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var file = await fileConversion.GetDownloadFileAsync(jobId, cancellationToken);
        return PhysicalFile(
            file.Path,
            file.ContentType,
            file.FileName,
            enableRangeProcessing: true);
    }

    private async Task WriteEventAsync(
        JobProgressEvent progressEvent,
        CancellationToken cancellationToken)
    {
        if (progressEvent.Name is not null)
        {
            await Response.WriteAsync($"event: {progressEvent.Name}\n", cancellationToken);
        }

        await Response.WriteAsync(
            $"data: {JsonSerializer.Serialize(progressEvent.Data, EventJsonOptions)}\n\n",
            cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}