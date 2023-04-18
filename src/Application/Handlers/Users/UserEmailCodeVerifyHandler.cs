using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Common.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Application.QueryableExtensions;
using LayeredTemplate.Domain.Entities;
using MediatR;

namespace LayeredTemplate.Application.Handlers.Users;

internal class UserEmailCodeVerifyHandler : IRequestHandler<UserEmailCodeVerifyRequest>
{
    private readonly IApplicationDbContext dbContext;
    private readonly ICurrentUserService currentUserService;
    private readonly IUserManager userManager;
    private readonly IUserPoolService userPoolService;

    public UserEmailCodeVerifyHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IUserManager userManager,
        IUserPoolService userPoolService)
    {
        this.dbContext = dbContext;
        this.currentUserService = currentUserService;
        this.userManager = userManager;
        this.userPoolService = userPoolService;
    }

    public async Task<Unit> Handle(UserEmailCodeVerifyRequest request, CancellationToken cancellationToken)
    {
        var user = await this.dbContext.Users.FindByIdOrDefault(this.currentUserService.UserId, cancellationToken);
        if (user == null)
        {
            throw new AppNotFoundException(nameof(User), this.currentUserService.UserId);
        }

        var verifyCodeResult = await this.userManager.VerifyChangeEmailCode(user, request.Email, request.Code);
        if (!verifyCodeResult)
        {
            throw new HttpStatusException("Invalid code.");
        }

        await this.userPoolService.UpdateUserProperties(new UserPoolUpdateUserRequest()
        {
            Id = user.Id,
            Email = request.Email,
            Phone = user.Phone,
            Role = user.Role,
        });

        user.Email = request.Email;
        await this.dbContext.SaveChangesAsync(cancellationToken);

        await this.userManager.UpdateSecurityStamp(user);

        return Unit.Value;
    }
}