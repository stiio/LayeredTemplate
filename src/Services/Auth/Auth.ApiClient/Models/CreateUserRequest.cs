namespace LayeredTemplate.Auth.ApiClient.Models;

public record CreateUserRequest
{
    public string Email { get; init; } = string.Empty;

    public bool EmailConfirmed { get; init; }

    public string? PhoneNumber { get; init; }

    public bool PhoneNumberConfirmed { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    /// <summary>Optional — omit for external-login-only accounts.</summary>
    public string? Password { get; init; }
}
