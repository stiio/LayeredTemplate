using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models.ApiKeys;
using LayeredTemplate.Application.Contracts.Requests.ApiKeys;
using LayeredTemplate.Application.QueryableExtensions;
using MediatR;

namespace LayeredTemplate.Application.Handlers.ApiKeys;

internal class ApiKeyGetSecretHandler : IRequestHandler<ApiKeySecretGetRequest, ApiKeySecretDto>
{
    private readonly IApplicationDbContext dbContext;
    private readonly IMapper mapper;

    public ApiKeyGetSecretHandler(
        IApplicationDbContext dbContext,
        IMapper mapper)
    {
        this.dbContext = dbContext;
        this.mapper = mapper;
    }

    public async Task<ApiKeySecretDto> Handle(ApiKeySecretGetRequest request, CancellationToken cancellationToken)
    {
        return await this.dbContext.ApiKeys
            .ProjectTo<ApiKeySecretDto>(this.mapper.ConfigurationProvider)
            .FirstById(request.Id, cancellationToken);
    }
}