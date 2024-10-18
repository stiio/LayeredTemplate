namespace LayeredTemplate.Infrastructure.Authorization;

public static class AuthorizeConstants
{
    public static class PolicyPrefix
    {
        public const string HasPermissionOnAction = "HasPermissionOnAction";

        public const string HasPermissionRequirement = "HasPermissionRequirement";
    }

    public static class RequirementAction
    {
        public const string Create = "Create";

        public const string Read = "Read";

        public const string Update = "Update";

        public const string Delete = "Delete";
    }

    public static class Roles
    {
        public const string Administrator = "Administrator";
    }
}