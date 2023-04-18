using LayeredTemplate.Domain.Common;
using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Domain.Entities;

public class User : IBaseAuditableEntity<Guid>
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public Role Role { get; set; }

    public string? SecurityStamp { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<TodoList>? TodoLists { get; set; }
}