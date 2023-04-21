using System.Security.Claims;

namespace LayeredTemplate.Shared.Constants;

public static class AppClaims
{
    public const string UserId = "userId";

    public const string Email = ClaimTypes.Email;

    public const string Phone = ClaimTypes.MobilePhone;

    public const string Role = ClaimTypes.Role;

    public const string EmailVerified = "email_verified";

    public const string PhoneNumberVerified = "phone_number_verified";

    public const string NameKey = ClaimTypes.Name;

    public const string FirstNameKey = ClaimTypes.GivenName;

    public const string LastNameKey = ClaimTypes.Surname;
}