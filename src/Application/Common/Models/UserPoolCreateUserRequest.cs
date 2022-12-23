namespace LayeredTemplate.Application.Common.Models;

public class UserPoolCreateUserRequest
{
    public string Email { get; set; } = null!;

    public bool NotSendEmail { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }

    public Dictionary<string, string>? AdditionalProperties { get; set; }
}