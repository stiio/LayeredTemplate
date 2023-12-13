namespace LayeredTemplate.Application.Features.ApiKeys.Models;

public class ApiKeySecretDto : ApiKeyDto
{
    public string Secret { get; set; } = null!;
}