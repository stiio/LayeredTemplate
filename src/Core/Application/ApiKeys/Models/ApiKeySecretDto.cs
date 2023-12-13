namespace LayeredTemplate.Application.ApiKeys.Models;

public class ApiKeySecretDto : ApiKeyDto
{
    public string Secret { get; set; } = null!;
}