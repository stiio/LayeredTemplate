using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.ApiKeys.Models;
using LayeredTemplate.Application.ApiKeys.Requests;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using MediatR;

namespace LayeredTemplate.Application.ApiKeys.Handlers;

internal class ApiKeyUpdateHandler : IRequestHandler<ApiKeyUpdateRequest, ApiKeyDto>
{
    private readonly IApplicationDbContext context;
    private readonly IMapper mapper;

    public ApiKeyUpdateHandler(
        IApplicationDbContext context,
        IMapper mapper)
    {
        this.context = context;
        this.mapper = mapper;
    }

    public async Task<ApiKeyDto> Handle(ApiKeyUpdateRequest request, CancellationToken cancellationToken)
    {
        var apiKey = await this.context.TodoLists.FirstById(request.Id, cancellationToken);

        this.mapper.Map(request.Body, apiKey);

        await this.context.SaveChangesAsync(cancellationToken);

        return await this.context.ApiKeys
            .ProjectTo<ApiKeyDto>(this.mapper.ConfigurationProvider)
            .FirstById(request.Id, cancellationToken);
    }
}