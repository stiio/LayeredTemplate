using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.Common.Interfaces;

public interface IUserManager
{
    Task<int> GenerateChangeEmailCode(User user, string email);

    Task<bool> VerifyChangeEmailCode(User user, string email, int code);

    Task<int> GenerateChangePhoneCode(User user, string phone);

    Task<bool> VerifyChangePhoneCode(User user, string phone, int code);

    Task UpdateSecurityStamp(User user);
}