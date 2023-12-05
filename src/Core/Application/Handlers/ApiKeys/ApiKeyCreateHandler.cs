using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models.ApiKeys;
using LayeredTemplate.Application.Contracts.Requests.ApiKeys;
using LayeredTemplate.Application.QueryableExtensions;
using LayeredTemplate.Domain.Entities;
using MediatR;

namespace LayeredTemplate.Application.Handlers.ApiKeys;

internal class ApiKeyCreateHandler : IRequestHandler<ApiKeyCreateRequest, ApiKeySecretDto>
{
    private readonly IApplicationDbContext dbContext;
    private readonly ICurrentUserService currentUserService;
    private readonly IMapper mapper;

    public ApiKeyCreateHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IMapper mapper)
    {
        this.dbContext = dbContext;
        this.currentUserService = currentUserService;
        this.mapper = mapper;
    }

    public async Task<ApiKeySecretDto> Handle(ApiKeyCreateRequest request, CancellationToken cancellationToken)
    {
        var apiKey = new ApiKey()
        {
            UserId = this.currentUserService.UserId,
            Name = request.Body.Name,
            Secret = Guid.NewGuid().ToString("N"),
        };

        await this.dbContext.ApiKeys.AddAsync(apiKey, cancellationToken);
        await this.dbContext.SaveChangesAsync(cancellationToken);

        return await this.dbContext.ApiKeys
            .ProjectTo<ApiKeySecretDto>(this.mapper.ConfigurationProvider)
            .FirstById(apiKey.Id, cancellationToken);
    }
}