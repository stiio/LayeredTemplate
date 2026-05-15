namespace LayeredTemplate.App.Shared.Auth.MockAuth;

/// <summary>
/// Bound from configuration <c>MockUserSettings</c> section. Drives <see cref="MockAuthHandler"/>
/// in Development. Public so integration tests can construct it directly.
/// </summary>
public sealed class MockUserSettings
{
    public string? Id { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Role { get; set; }
}
