using System.Security.Claims;

namespace LayeredTemplate.App.Shared.Auth;

/// <summary>
/// Strongly-typed view over the current request's <see cref="ClaimsPrincipal"/>.
/// Inject as a constructor / parameter dependency in endpoints. Single-line implementation
/// reading from <see cref="IHttpContextAccessor"/> — no extra abstractions over what's already there.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }

    string Email { get; }

    bool EmailVerified { get; }

    string? Phone { get; }

    bool PhoneVerified { get; }

    string? FirstName { get; }

    string? LastName { get; }

    string? Name { get; }
}

internal sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal User => this.httpContextAccessor.HttpContext!.User!;

    public Guid UserId => new(this.User.FindFirst(AppClaims.UserId)!.Value);

    public string Email => this.User.FindFirst(AppClaims.Email)!.Value;

    public bool EmailVerified => this.User.FindFirstValue(AppClaims.EmailVerified) == "true";

    public string? Phone => this.User.FindFirst(AppClaims.Phone)?.Value;

    public bool PhoneVerified => this.User.FindFirstValue(AppClaims.PhoneVerified) == "true";

    public string? FirstName => this.User.FindFirstValue(AppClaims.FirstName);

    public string? LastName => this.User.FindFirstValue(AppClaims.LastName);

    public string? Name => this.User.FindFirstValue(AppClaims.Name);
}
