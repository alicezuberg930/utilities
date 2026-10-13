using System.Text.Json;
using System.Text.Json.Serialization;

namespace Utilities.Application.DTOs.Responses;

public sealed record YoutubeMetadataResponse(
    string? Title,
    string? Thumbnail,
    IReadOnlyList<JsonElement> VideoFormats,
    IReadOnlyList<JsonElement> AudioFormats,
    [property: JsonPropertyName("combinedFormat")] string? CombinedFormat);
