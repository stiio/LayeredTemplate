namespace LayeredTemplate.App.Shared.Authorization;

public static class AppClaims
{
    public const string UserId = "sub";

    public const string Email = "email";

    public const string EmailVerified = "email_verified";

    public const string Phone = "phone_number";

    public const string PhoneVerified = "phone_number_verified";

    public const string Name = "name";

    public const string FirstName = "given_name";

    public const string LastName = "family_name";

    public const string Permissions = "app:permissions";
}