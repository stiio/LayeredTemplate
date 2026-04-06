using System.Security.Claims;

namespace LayeredTemplate.Plugins.Authorization.Abstractions.Constants;

public static class AppClaims
{
    public const string UserId = "userId";

    public const string Email = ClaimTypes.Email;

    public const string Phone = ClaimTypes.MobilePhone;

    public const string EmailVerified = "email_verified";

    public const string PhoneVerified = "phone_verified";

    public const string Name = ClaimTypes.Name;

    public const string FirstName = ClaimTypes.GivenName;

    public const string LastName = ClaimTypes.Surname;

    public const string Permissions = "app:permissions";
}