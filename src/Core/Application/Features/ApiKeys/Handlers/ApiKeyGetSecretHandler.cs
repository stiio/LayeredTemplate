using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.ApiKeys.Models;
using LayeredTemplate.Application.Features.ApiKeys.Requests;
using MediatR;

namespace LayeredTemplate.Application.Features.ApiKeys.Handlers;

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