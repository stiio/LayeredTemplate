using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Application.Contracts.Models.ApiKeys;

public class ApiKeyCreateRequestBody
{
    [MaxLength(255)]
    public string Name { get; set; } = null!;
}