using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Application.ApiKeys.Models;

public class ApiKeyCreateRequestBody
{
    [MaxLength(255)]
    public string Name { get; set; } = null!;
}