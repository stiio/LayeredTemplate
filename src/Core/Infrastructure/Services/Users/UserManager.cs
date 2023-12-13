using System.Text;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Users.Services;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Infrastructure.Utilities;

namespace LayeredTemplate.Infrastructure.Services.Users;

internal class UserManager : IUserManager
{
    private const string ChangeEmailPurpose = "ChangeEmail";
    private const string ChangePhonePurpose = "ChangePhone";
    private readonly IApplicationDbContext dbContext;

    public UserManager(IApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public Task<int> GenerateChangeEmailCode(User user, string email)
    {
        return this.GenerateCode(user, $"{ChangeEmailPurpose}:{email}");
    }

    public Task<bool> VerifyChangeEmailCode(User user, string email, int code)
    {
        return this.VerifyCode(user, code, $"{ChangeEmailPurpose}:{email}");
    }

    public Task<int> GenerateChangePhoneCode(User user, string phone)
    {
        return this.GenerateCode(user, $"{ChangePhonePurpose}:{phone}");
    }

    public Task<bool> VerifyChangePhoneCode(User user, string phone, int code)
    {
        return this.VerifyCode(user, code, $"{ChangePhonePurpose}:{phone}");
    }

    public async Task UpdateSecurityStamp(User user)
    {
        user.SecurityStamp = Base32.ToBase32(Rfc6238Service.GenerateRandomKey());
        await this.dbContext.SaveChangesAsync();
    }

    private async Task<int> GenerateCode(User user, string purpose)
    {
        var secretToken = await this.GetSecretToken(user);
        return Rfc6238Service.GenerateCode(secretToken, purpose);
    }

    private async Task<bool> VerifyCode(User user, int code, string purpose)
    {
        var secretToken = await this.GetSecretToken(user);
        return Rfc6238Service.ValidateCode(secretToken, code, purpose);
    }

    private async Task<byte[]> GetSecretToken(User user)
    {
        if (user.SecurityStamp is null)
        {
            await this.UpdateSecurityStamp(user);
        }

        return Encoding.Unicode.GetBytes(user.SecurityStamp!);
    }
}