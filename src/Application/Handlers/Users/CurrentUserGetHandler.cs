using System.Net;
using AutoMapper;
using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Application.QueryableExtensions;
using LayeredTemplate.Domain.Entities;
using MediatR;

namespace LayeredTemplate.Application.Handlers.Users;

internal class CurrentUserGetHandler : IRequestHandler<CurrentUserGetRequest, CurrentUser>
{
    private readonly ICurrentUserService currentUserService;
    private readonly IUserPoolService userPoolService;
    private readonly IApplicationDbContext dbContext;
    private readonly IMapper mapper;

    public CurrentUserGetHandler(
        ICurrentUserService currentUserService,
        IUserPoolService userPoolService,
        IApplicationDbContext dbContext,
        IMapper mapper)
    {
        this.currentUserService = currentUserService;
        this.userPoolService = userPoolService;
        this.dbContext = dbContext;
        this.mapper = mapper;
    }

    public async Task<CurrentUser> Handle(CurrentUserGetRequest request, CancellationToken cancellationToken)
    {
        var user = await this.dbContext.Users.FirstByIdOrDefault(this.currentUserService.UserId, cancellationToken);
        if (user is null)
        {
            if (!await this.userPoolService.ExistsUser(this.currentUserService.UserId))
            {
                throw new HttpStatusException("User does not exists.", HttpStatusCode.Unauthorized);
            }

            user = new User()
            {
                Id = this.currentUserService.UserId,
                Email = this.currentUserService.Email,
                EmailVerified = this.currentUserService.EmailVerified,
                Phone = this.currentUserService.Phone,
                PhoneVerified = this.currentUserService.PhoneVerified,
                Role = this.currentUserService.Role,
                FirstName = this.currentUserService.FirstName,
                LastName = this.currentUserService.LastName,
            };

            await this.dbContext.Users.AddAsync(user, cancellationToken);
            await this.dbContext.SaveChangesAsync(cancellationToken);
        }

        return this.mapper.Map<CurrentUser>(user);
    }
}