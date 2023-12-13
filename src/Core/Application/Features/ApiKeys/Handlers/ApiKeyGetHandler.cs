using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.ApiKeys.Models;
using LayeredTemplate.Application.Features.ApiKeys.Requests;
using MediatR;

namespace LayeredTemplate.Application.Features.ApiKeys.Handlers;

internal class ApiKeyGetHandler : IRequestHandler<ApiKeyGetRequest, ApiKeyDto>
{
    private readonly IApplicationDbContext dbContext;
    private readonly IMapper mapper;

    public ApiKeyGetHandler(
        IApplicationDbContext dbContext,
        IMapper mapper)
    {
        this.dbContext = dbContext;
        this.mapper = mapper;
    }

    public async Task<ApiKeyDto> Handle(ApiKeyGetRequest request, CancellationToken cancellationToken)
    {
        return await this.dbContext.ApiKeys
            .ProjectTo<ApiKeyDto>(this.mapper.ConfigurationProvider)
            .FirstById(request.Id, cancellationToken);
    }
}