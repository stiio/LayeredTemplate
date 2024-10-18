using System.Security.Claims;
using LayeredTemplate.App.Application.Features.Users.Services;
using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Http;

namespace LayeredTemplate.App.Infrastructure.Services.Users;

internal class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId => new(this.httpContextAccessor.HttpContext!.User!.FindFirst(AppClaims.UserId)!.Value);

    public string Email => this.httpContextAccessor.HttpContext!.User!.FindFirst(AppClaims.Email)!.Value;

    public bool EmailVerified => this.httpContextAccessor.HttpContext!.User!.FindFirstValue(AppClaims.EmailVerified) == "true";

    public string? Phone => this.httpContextAccessor.HttpContext!.User!.FindFirst(AppClaims.Phone)?.Value;

    public bool PhoneVerified => this.httpContextAccessor.HttpContext!.User!.FindFirstValue(AppClaims.PhoneVerified) == "true";

    public string? FirstName => this.httpContextAccessor.HttpContext!.User!.FindFirstValue(AppClaims.FirstName);

    public string? LastName => this.httpContextAccessor.HttpContext!.User!.FindFirstValue(AppClaims.LastName);

    public string? Name => this.httpContextAccessor.HttpContext!.User!.FindFirstValue(AppClaims.Name);
}