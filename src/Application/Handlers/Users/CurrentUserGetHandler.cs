using System.Net;
using AutoMapper;
using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Application.Events.Users;
using LayeredTemplate.Domain.Entities;
using MediatR;

namespace LayeredTemplate.Application.Handlers.Users;

internal class CurrentUserGetHandler : IRequestHandler<CurrentUserGetRequest, CurrentUser>
{
    private readonly ICurrentUserService currentUserService;
    private readonly IUserPoolService userPoolService;
    private readonly IApplicationDbContext dbContext;
    private readonly IMapper mapper;
    private readonly IPublisher publisher;

    public CurrentUserGetHandler(
        ICurrentUserService currentUserService,
        IUserPoolService userPoolService,
        IApplicationDbContext dbContext,
        IMapper mapper,
        IPublisher publisher)
    {
        this.currentUserService = currentUserService;
        this.userPoolService = userPoolService;
        this.dbContext = dbContext;
        this.mapper = mapper;
        this.publisher = publisher;
    }

    public async Task<CurrentUser> Handle(CurrentUserGetRequest request, CancellationToken cancellationToken)
    {
        var user = await this.dbContext.Users.FindAsync(this.currentUserService.UserId);
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
                Role = this.currentUserService.Role,
            };

            await this.dbContext.Users.AddAsync(user, cancellationToken);
            await this.dbContext.SaveChangesAsync(cancellationToken);

            await this.publisher.Publish(new UserCreatedEvent(user.Id), cancellationToken);
        }

        this.ThrowUnauthorizedIfAttributesNotMatch(user);

        return this.mapper.Map<CurrentUser>(user);
    }

    private void ThrowUnauthorizedIfAttributesNotMatch(User user)
    {
        if (user.Id == this.currentUserService.UserId
            && (user.Email != this.currentUserService.Email
                || user.Phone != this.currentUserService.Phone
                || user.Role != this.currentUserService.Role))
        {
            throw new HttpStatusException("Attributes in token and database do not match.", HttpStatusCode.Unauthorized);
        }
    }
}