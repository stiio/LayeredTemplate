namespace LayeredTemplate.Application.Features.Users.Services;

public interface ICurrentUserService
{
    public Guid UserId { get; }

    public string Email { get; }

    public bool EmailVerified { get; }

    public string? Phone { get; }

    public bool PhoneVerified { get; }

    public string? FirstName { get; }

    public string? LastName { get; }

    public string? Name { get; }
}