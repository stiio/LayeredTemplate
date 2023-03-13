using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Application.Common.Models;

public class UserPoolUpdateUserRequest
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public Role Role { get; set; }
}