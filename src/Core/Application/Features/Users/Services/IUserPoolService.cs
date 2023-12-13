using LayeredTemplate.Application.Features.Users.Models;

namespace LayeredTemplate.Application.Features.Users.Services;

public interface IUserPoolService
{
    Task<Guid> CreateUser(UserPoolCreateUserRequest request);

    Task UpdateUserProperties(UserPoolUpdateUserRequest request);

    Task<bool> ExistsUser(Guid userId);

    Task DeleteUser(Guid userId);
}