using LayeredTemplate.Domain.Common;

namespace LayeredTemplate.Domain.Entities;

public class AuditEvent : IBaseEntity<Guid>
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = null!;

    public string? Data { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}