using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Auth.Web.Controllers.Api.Admin;

public record UserResponse(
    string Id,
    string Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    bool HasPassword);

public class CreateUserRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(128)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Optional — omit for external-login-only accounts.</summary>
    [MinLength(6)]
    [MaxLength(100)]
    public string? Password { get; set; }

    public bool EmailConfirmed { get; set; }

    [Phone]
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }
}

public class UpdateUserRequest
{
    /// <summary>Only <c>true</c> is meaningful — admin confirms email. Omit or <c>null</c> to leave as-is; <c>false</c> is rejected.</summary>
    public bool? EmailConfirmed { get; set; }

    /// <summary>Set to change the user's phone. PhoneNumberConfirmed is reset to false.</summary>
    [Phone]
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>Set to replace the user's password manually. Must pass Identity password policy.</summary>
    [MinLength(6)]
    [MaxLength(100)]
    public string? NewPassword { get; set; }
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
