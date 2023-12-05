using LayeredTemplate.Domain.Common;

namespace LayeredTemplate.Domain.Entities;

public class ApiKey : IBaseAuditableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string Name { get; set; } = null!;

    public string Secret { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}