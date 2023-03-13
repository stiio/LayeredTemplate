using LayeredTemplate.Application.Common.Models;
using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Application.Common.Interfaces;

public interface IUserPoolService
{
    Task<Guid> CreateUser(UserPoolCreateUserRequest request);

    Task UpdateUserProperties(UserPoolUpdateUserRequest request);

    Task<bool> ExistsUser(Guid userId);

    Task DeleteUser(Guid userId);
}