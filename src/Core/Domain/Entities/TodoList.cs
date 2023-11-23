using LayeredTemplate.Domain.Common;
using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Domain.Entities;

public class TodoList : IBaseAuditableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string? Name { get; set; }

    public TodoListType Type { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}