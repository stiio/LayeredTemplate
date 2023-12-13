using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.ApiKeys.Models;
using LayeredTemplate.Application.ApiKeys.Requests;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Users.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Application.ApiKeys.Handlers;

internal class ApiKeyListHandler : IRequestHandler<ApiKeyListRequest, ApiKeyDto[]>
{
    private readonly IApplicationDbContext dbContext;
    private readonly ICurrentUserService currentUserService;
    private readonly IMapper mapper;

    public ApiKeyListHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IMapper mapper)
    {
        this.dbContext = dbContext;
        this.currentUserService = currentUserService;
        this.mapper = mapper;
    }

    public Task<ApiKeyDto[]> Handle(ApiKeyListRequest request, CancellationToken cancellationToken)
    {
        return this.dbContext.ApiKeys
            .Where(x => x.UserId == this.currentUserService.UserId)
            .ProjectTo<ApiKeyDto>(this.mapper.ConfigurationProvider)
            .OrderByDescending(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }
}