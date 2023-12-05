using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models.ApiKeys;
using LayeredTemplate.Application.Contracts.Requests.ApiKeys;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Application.Handlers.ApiKeys;

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