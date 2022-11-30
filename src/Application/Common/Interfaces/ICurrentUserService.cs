using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Application.Common.Interfaces;

public interface ICurrentUserService
{
    public Guid UserId { get; }

    public string? Email { get; }

    public bool IsAuthenticate { get; }

    public Role Role { get; }
}