using LayeredTemplate.Domain.Common;
using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Domain.Entities;

public class User : BaseEntity
{
    public string? Name { get; set; }

    public string? Email { get; set; }

    public Role Role { get; set; }

    public ICollection<TodoList>? TodoLists { get; set; }
}