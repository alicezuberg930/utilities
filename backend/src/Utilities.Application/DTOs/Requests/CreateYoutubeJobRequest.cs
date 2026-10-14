using System.ComponentModel.DataAnnotations;

namespace Utilities.Application.DTOs.Requests;

public sealed record CreateYoutubeJobRequest(
    [Required] string Url,
    double? AudioQuality,
    string? VideoQuality,
    [Required] string Format);
