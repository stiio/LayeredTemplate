namespace LayeredTemplate.App.Infrastructure.Authorization;

public static class AuthorizeConstants
{
    public static class PolicyPrefix
    {
        public const string HasPermissionOnAction = "HasPermissionOnAction";

        public const string HasPermissionRequirement = "HasPermissionRequirement";
    }

    public static class Roles
    {
        public const string Administrator = "Administrator";
    }
}