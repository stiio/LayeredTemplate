using LayeredTemplate.App.Shared.Db;

namespace LayeredTemplate.App.Features.Users.Entities;

/// <summary>
/// User aggregate. Public because integration tests construct seed users directly — keep
/// constructor surface minimal so seed code stays compact.
/// </summary>
public sealed class User : IBaseAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

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
