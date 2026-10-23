using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Utilities.Api.Filters;
using Utilities.Application.DTOs.Requests;
using Utilities.Application.DTOs.Responses;
using Utilities.Application.Exceptions;
using Utilities.Application.Interfaces;
using Utilities.Infrastructure.Configuration;

namespace Utilities.Api.Controllers.V1;

[ApiController]
[Route("api/v1/image-compression")]
public sealed class ImageCompressionController(
    IImageCompression imageCompression,
    IImageCompressionProgressStream progressStream,
    IOptions<FileConversionOptions> options) : ControllerBase
{
    private const long MaximumImageBytes = 20L * 1024 * 1024;
    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly FileConversionOptions _options = options.Value;

    [HttpPost("job")]
    [Consumes("multipart/form-data")]
    [FileConversionUploadLimits]
    [ResponseEnvelope(StatusCodes.Status201Created, "Image compression job created successfully")]
    public async Task<ImageCompressionJobResponse> CreateJobAsync(
        [FromForm] IFormFile? file,
        [FromForm] int quality,
        [FromForm] bool convertToWebp,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw new RequestValidationException("A source image is required.");
        }

        var maximumBytes = Math.Min(MaximumImageBytes, _options.MaxUploadFileBytes);
        if (file.Length > maximumBytes)
        {
            throw new RequestValidationException(
                $"The source image exceeds the {maximumBytes / (1024 * 1024)} MB upload limit.");
        }

        await using var content = file.OpenReadStream();
        return await imageCompression.CreateJobAsync(
            new CreateImageCompressionRequest(
                file.FileName,
                file.Length,
                quality,
                convertToWebp,
                content),
            cancellationToken);
    }

    [HttpGet("progress/{jobId:guid}")]
    [SkipResponseEnvelope]
    public async Task StreamProgressAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var subscription = progressStream.Subscribe(jobId);
        var job = await imageCompression.GetJobStateAsync(jobId, cancellationToken) ??
            throw new ResourceNotFoundException("Image compression job was not found.");

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        await Response.StartAsync(cancellationToken);
        await WriteEventAsync(
            new JobProgressEvent(null, new JobProgress(job.Progress)),
            cancellationToken);

        if (job.IsCompleted)
        {
            await WriteEventAsync(new JobProgressEvent("complete", "done"), cancellationToken);
            return;
        }

        if (job.IsFailed)
        {
            await WriteEventAsync(
                new JobProgressEvent(
                    "error",
                    new JobFailure(job.Error ?? "The image compression failed.")),
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
        var file = await imageCompression.GetDownloadFileAsync(jobId, cancellationToken);
        return PhysicalFile(
            file.Path,
            file.ContentType,
            file.FileName,
            enableRangeProcessing: true);
    }

    private async Task WriteEventAsync(JobProgressEvent progressEvent, CancellationToken cancellationToken)
    {
        if (progressEvent.Name is not null)
        {
            await Response.WriteAsync($"event: {progressEvent.Name}\n", cancellationToken);
        }

        await Response.WriteAsync($"data: {JsonSerializer.Serialize(progressEvent.Data, EventJsonOptions)}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
