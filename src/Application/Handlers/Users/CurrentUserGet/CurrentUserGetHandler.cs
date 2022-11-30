using AutoMapper;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Application.Events.Users;
using LayeredTemplate.Domain.Entities;
using MediatR;

namespace LayeredTemplate.Application.Handlers.Users.CurrentUserGet;

internal class CurrentUserGetHandler : IRequestHandler<CurrentUserGetRequest, CurrentUser>
{
    private readonly ICurrentUserService currentUserService;
    private readonly IApplicationDbContext dbContext;
    private readonly IMapper mapper;
    private readonly IPublisher publisher;

    public CurrentUserGetHandler(
        ICurrentUserService currentUserService,
        IApplicationDbContext dbContext,
        IMapper mapper,
        IPublisher publisher)
    {
        this.currentUserService = currentUserService;
        this.dbContext = dbContext;
        this.mapper = mapper;
        this.publisher = publisher;
    }

    public async Task<CurrentUser> Handle(CurrentUserGetRequest request, CancellationToken cancellationToken)
    {
        var user = await this.dbContext.Users.FindAsync(this.currentUserService.UserId);

        if (user is null)
        {
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

        return this.mapper.Map<CurrentUser>(user);
    }
}