using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Application.Features.ApiKeys.Models;

public class ApiKeyUpdateRequestBody
{
    [MaxLength(255)]
    public string Name { get; set; } = null!;
}