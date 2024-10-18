using LayeredTemplate.App.Domain.Common;

namespace LayeredTemplate.App.Domain.Entities;

public class User : IBaseAuditableEntity
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public bool EmailVerified { get; set; }

    public string? Phone { get; set; }

    public bool PhoneVerified { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? SecurityStamp { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}