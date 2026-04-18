namespace LayeredTemplate.Auth.ApiClient.Models;

public record UpdateUserRequest
{
    /// <summary>Only <c>true</c> is meaningful. Omit or <c>null</c> to leave as-is; <c>false</c> is rejected by the server.</summary>
    public bool? EmailConfirmed { get; init; }

    /// <summary>Set to change the user's phone. Server resets PhoneNumberConfirmed to false.</summary>
    public string? PhoneNumber { get; init; }

    public bool? PhoneNumberConfirmed { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    /// <summary>Set to replace the user's password. Must pass the server's password policy.</summary>
    public string? NewPassword { get; init; }
}
