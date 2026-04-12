using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Application.Common.Models;

public class AppProblemDetails : ProblemDetails
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(100)]
    public AppErrorType? ErrorType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Errors { get; set; }
}
