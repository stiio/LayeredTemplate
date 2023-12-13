using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.ApiKeys.Requests;
using MediatR;

namespace LayeredTemplate.Application.Features.ApiKeys.Handlers;

internal class ApiKeyDeleteHandler : IRequestHandler<ApiKeyDeleteRequest>
{
    private readonly IApplicationDbContext dbContext;

    public ApiKeyDeleteHandler(IApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task Handle(ApiKeyDeleteRequest request, CancellationToken cancellationToken)
    {
        var apiKey = await this.dbContext.ApiKeys.FindAsync(request.Id);

        this.dbContext.ApiKeys.Remove(apiKey!);
        await this.dbContext.SaveChangesAsync(cancellationToken);
    }
}