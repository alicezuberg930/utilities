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
[Route("api/v1/youtube")]
public sealed class YoutubeController(
    IYoutubeService youtubeService,
    IYoutubeProgressStream progressStream) : ControllerBase
{
    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("info")]
    public Task<YoutubeMetadataResponse> GetInfoAsync(
        [FromQuery] string? url,
        CancellationToken cancellationToken) =>
        youtubeService.GetInfoAsync(url, cancellationToken);

    [HttpPost("job")]
    [ResponseEnvelope(StatusCodes.Status201Created, "Job created successfully")]
    public Task<YoutubeJobResponse> CreateJobAsync(
        [FromBody] CreateYoutubeJobRequest request,
        CancellationToken cancellationToken) =>
        youtubeService.CreateJobAsync(request, cancellationToken);

    [HttpGet("progress/{jobId:guid}")]
    [SkipResponseEnvelope]
    public async Task StreamProgressAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var subscription = progressStream.Subscribe(jobId);
        var job = await youtubeService.GetJobStateAsync(jobId, cancellationToken)
            ?? throw new ResourceNotFoundException("YouTube job was not found.");

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        await Response.StartAsync(cancellationToken);
        await WriteEventAsync(
            new YoutubeProgressEvent(null, new YoutubeProgress(job.Progress)),
            cancellationToken);

        if (job.IsCompleted)
        {
            await WriteEventAsync(new YoutubeProgressEvent("complete", "done"), cancellationToken);
            return;
        }

        if (job.IsFailed)
        {
            await WriteEventAsync(
                new YoutubeProgressEvent(
                    "error",
                    new YoutubeFailure(job.Error ?? "The download failed.")),
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
        var file = await youtubeService.GetDownloadFileAsync(jobId, cancellationToken);
        return PhysicalFile(
            file.Path,
            file.ContentType,
            file.FileName,
            enableRangeProcessing: true);
    }

    private async Task WriteEventAsync(
        YoutubeProgressEvent progressEvent,
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
