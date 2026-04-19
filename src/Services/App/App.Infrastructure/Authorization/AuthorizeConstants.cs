namespace LayeredTemplate.App.Infrastructure.Authorization;

public static class AuthorizeConstants
{
    public static class PolicyPrefix
    {
        public const string HasPermission = "HasPermission";

        public const string HasScope = "HasScope";
    }

    public static class Roles
    {
        public const string Administrator = "Administrator";
    }
}