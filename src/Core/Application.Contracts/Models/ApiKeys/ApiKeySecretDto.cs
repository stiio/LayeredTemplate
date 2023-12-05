namespace LayeredTemplate.Application.Contracts.Models.ApiKeys;

public class ApiKeySecretDto : ApiKeyDto
{
    public string Secret { get; set; } = null!;
}