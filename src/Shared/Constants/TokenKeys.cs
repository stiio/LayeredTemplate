namespace LayeredTemplate.Shared.Constants;

public static class TokenKeys
{
    public const string UserId = "cognito:username";

    public const string Email = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

    public const string Phone = "phone_number";

    public const string Role = "cognito:groups";

    public const string EmailVerified = "email_verified";

    public const string PhoneNumberVerified = "phone_number_verified";

    public const string NameKey = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

    public const string FirstNameKey = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname";

    public const string LastNameKey = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname";
}