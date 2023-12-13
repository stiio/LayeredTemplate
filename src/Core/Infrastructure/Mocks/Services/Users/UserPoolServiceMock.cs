using LayeredTemplate.Application.Users.Models;
using LayeredTemplate.Application.Users.Services;

namespace LayeredTemplate.Infrastructure.Mocks.Services.Users;

internal class UserPoolServiceMock : IUserPoolService
{
    public Task<Guid> CreateUser(UserPoolCreateUserRequest request)
    {
        return Task.FromResult(Guid.NewGuid());
    }

    public Task UpdateUserProperties(UserPoolUpdateUserRequest request)
    {
        return Task.CompletedTask;
    }

    public Task<bool> ExistsUser(Guid userId)
    {
        return Task.FromResult(true);
    }

    public Task DeleteUser(Guid userId)
    {
        return Task.CompletedTask;
    }
}