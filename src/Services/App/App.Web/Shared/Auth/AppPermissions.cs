namespace LayeredTemplate.App.Shared.Auth;

/// <summary>
/// Custom permissions enum. Used by <see cref="HasPermissionAttribute"/> to gate endpoints
/// against a user's <c>app:permissions</c> claim.
/// </summary>
public enum AppPermissions
{
    UserRead = 0x10,
    UserWrite = 0x11,
}