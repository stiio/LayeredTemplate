using LayeredTemplate.Application.Common.Models;
using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Application.Common.Interfaces;

public interface IUserPoolService
{
    Task<Guid> CreateUser(UserPoolCreateUserRequest request);

    Task AddUserToGroup(Guid userId, Role role);

    Task RemoveUserFromGroup(Guid userId, Role role);
}