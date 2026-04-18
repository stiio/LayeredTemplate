using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Plugins.PhoneHelpers.Attributes;

namespace LayeredTemplate.Auth.Web.Controllers.Api.Admin;

public record UserResponse(
    string Id,
    string Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    string? FirstName,
    string? LastName,
    bool HasPassword,
    string[] Roles);

public record CreateUserRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(128)]
    public string Email { get; init; } = string.Empty;

    public bool EmailConfirmed { get; init; }

    [Phone]
    [MaxLength(20)]
    [NormalizedPhone]
    public string? PhoneNumber { get; init; }

    public bool PhoneNumberConfirmed { get; init; }

    [MaxLength(100)]
    public string? FirstName { get; init; }

    [MaxLength(100)]
    public string? LastName { get; init; }

    /// <summary>Optional — omit for external-login-only accounts.</summary>
    [MinLength(6)]
    [MaxLength(100)]
    public string? Password { get; init; }
}

public record UpdateUserRequest
{
    /// <summary>Only <c>true</c> is meaningful — admin confirms email. Omit or <c>null</c> to leave as-is; <c>false</c> is rejected.</summary>
    public bool? EmailConfirmed { get; init; }

    /// <summary>Set to change the user's phone. PhoneNumberConfirmed is reset to false.</summary>
    [Phone]
    [MaxLength(20)]
    [NormalizedPhone]
    public string? PhoneNumber { get; init; }

    public bool? PhoneNumberConfirmed { get; init; }

    [MaxLength(100)]
    public string? FirstName { get; init; }

    [MaxLength(100)]
    public string? LastName { get; init; }

    /// <summary>Set to replace the user's password manually. Must pass Identity password policy.</summary>
    [MinLength(6)]
    [MaxLength(100)]
    public string? NewPassword { get; init; }
}

public record ErrorResponse(string Error, IReadOnlyList<string>? Details = null);

/// <summary>
/// One-time token for inviting a newly-provisioned user to set their password and sign in.
/// The backend composes its own invite email with a link like
/// <c>{AuthWeb}/account/accept_invite?userId={UserId}&amp;code={Token}&amp;returnUrl=...</c>.
/// </summary>
public record InviteTokenResponse(
    string UserId,
    string Token,
    DateTimeOffset ExpiresAt);
