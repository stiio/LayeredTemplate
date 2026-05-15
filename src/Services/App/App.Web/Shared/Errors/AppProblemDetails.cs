using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Shared.Errors;

/// <summary>
/// Extends RFC 7807 <see cref="ProblemDetails"/> with an app-specific <see cref="ErrorType"/>
/// enum and field-level <see cref="Errors"/> dictionary (matches
/// <see cref="ValidationProblemDetails.Errors"/> shape so clients can use the same parser).
/// </summary>
public sealed class AppProblemDetails : ProblemDetails
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(100)]
    public AppErrorType? ErrorType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Errors { get; set; }
}
