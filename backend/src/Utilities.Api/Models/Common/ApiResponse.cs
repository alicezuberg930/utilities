using System.Text.Json.Serialization;

namespace Utilities.Api.Models.Common;

public sealed record ApiResponse<T>(int StatusCode, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message, T Data);

public sealed record ApiErrorResponse(int StatusCode, string Message);
