using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Application.Common.Models;

public class UserPoolCreateUserRequest
{
    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public Role Role { get; set; }

    public bool NotSendEmail { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }

    public Dictionary<string, string>? AdditionalProperties { get; set; }
}