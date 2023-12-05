using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Application.Contracts.Models.ApiKeys;

public class ApiKeyUpdateRequestBody
{
    [MaxLength(255)]
    public string Name { get; set; } = null!;
}