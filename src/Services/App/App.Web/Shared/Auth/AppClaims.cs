namespace LayeredTemplate.App.Shared.Auth;

public static class AppAuthenticationSchemes
{
    public const string Bearer = "Bearer";

    public const string ApiKey = "ApiKey";
}

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

public static class AppRoles
{
    public const string Admin = "Admin";
}

/// <summary>
/// Custom permissions enum. Used by <see cref="HasPermissionAttribute"/> to gate endpoints
/// against a user's <c>app:permissions</c> claim.
/// </summary>
public enum AppPermissions
{
    UserRead = 0x10,
    UserWrite = 0x11,
}
