namespace LayeredTemplate.App.Features.Users.Models;

public sealed class CurrentUserDto
{
    /// <summary>Id of user</summary>
    public Guid Id { get; init; }

    /// <summary>Email of user</summary>
    /// <example>example@email.com</example>
    public string? Email { get; init; }

    public bool EmailVerified { get; init; }

    public string? Phone { get; init; }

    public bool PhoneVerified { get; init; }

    /// <example>John</example>
    public string? FirstName { get; init; }

    /// <example>Doe</example>
    public string? LastName { get; init; }
}

public sealed class UserShortInfoDto
{
    public Guid Id { get; init; }

    public string? Email { get; init; }
}
