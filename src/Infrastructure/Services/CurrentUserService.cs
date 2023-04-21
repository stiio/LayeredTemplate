using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Domain.Enums;
using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Http;

namespace LayeredTemplate.Infrastructure.Services;

internal class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId => new(this.httpContextAccessor.HttpContext!.User!.FindFirst(AppClaims.UserId)!.Value);

    public string? Email => this.httpContextAccessor.HttpContext!.User!.FindFirst(AppClaims.Email)?.Value;

    public string? Phone => this.httpContextAccessor.HttpContext!.User!.FindFirst(AppClaims.Phone)?.Value;

    public bool IsAuthenticate => this.httpContextAccessor.HttpContext?.User.Identity is { IsAuthenticated: true, AuthenticationType: AppAuthenticationTypes.OAuth };

    public Role Role => this.GetRole();

    private Role GetRole()
    {
        var role = this.httpContextAccessor.HttpContext!.User!.FindFirst(AppClaims.Role)?.Value;

        return string.IsNullOrEmpty(role)
            ? Role.Guest
            : Enum.Parse<Role>(role, true);
    }
}