﻿using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.Users.Requests;
using LayeredTemplate.Application.Features.Users.Services;
using LayeredTemplate.Domain.Entities;
using MediatR;

namespace LayeredTemplate.Application.Features.Users.Handlers;

internal class UserEmailCodeSendHandler : IRequestHandler<UserEmailCodeSendRequest>
{
    private readonly IApplicationDbContext dbContext;
    private readonly ICurrentUserService currentUserService;
    private readonly IUserManager userManager;
    private readonly IEmailSender emailSender;

    public UserEmailCodeSendHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IUserManager userManager,
        IEmailSender emailSender)
    {
        this.dbContext = dbContext;
        this.currentUserService = currentUserService;
        this.userManager = userManager;
        this.emailSender = emailSender;
    }

    public async Task Handle(UserEmailCodeSendRequest request, CancellationToken cancellationToken)
    {
        var user = await this.dbContext.Users.FirstByIdOrDefault(this.currentUserService.UserId, cancellationToken);
        if (user == null)
        {
            throw new AppNotFoundException(nameof(User), this.currentUserService.UserId);
        }

        var code = await this.userManager.GenerateChangeEmailCode(user, request.Email);
        await this.emailSender.SendEmail(request.Email, "Change Email", code.ToString());
    }
}