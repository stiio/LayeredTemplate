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

    public Guid UserId => new(this.httpContextAccessor.HttpContext!.User!.FindFirst(TokenKeys.UserId)!.Value);

    public string? Email => this.httpContextAccessor.HttpContext!.User!.FindFirst(TokenKeys.Email)?.Value;

    public string? Phone => this.httpContextAccessor.HttpContext!.User!.FindFirst(TokenKeys.Phone)?.Value;

    public bool IsAuthenticate => this.httpContextAccessor.HttpContext?.User.Identity is { IsAuthenticated: true, AuthenticationType: AppAuthenticationTypes.User };

    public Role Role => this.GetRole();

    private Role GetRole()
    {
        var role = this.httpContextAccessor.HttpContext!.User!.FindFirst(TokenKeys.Role)?.Value;

        if (string.IsNullOrEmpty(role))
        {
            return Role.Guest;
        }

        return Enum.Parse<Role>(role, true);
    }
}