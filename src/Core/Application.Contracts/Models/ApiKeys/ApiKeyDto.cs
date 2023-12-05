using LayeredTemplate.Application.Contracts.Models.Users;

namespace LayeredTemplate.Application.Contracts.Models.ApiKeys;

public class ApiKeyDto
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public UserShortInfo? User { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}