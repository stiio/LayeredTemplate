using LayeredTemplate.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Application.Common.Behaviors;

public class RollbackTransactionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IApplicationDbContext dbContext;
    private readonly ILogger<RollbackTransactionBehaviour<TRequest, TResponse>> logger;

    public RollbackTransactionBehaviour(
        IApplicationDbContext dbContext,
        ILogger<RollbackTransactionBehaviour<TRequest, TResponse>> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception)
        {
            this.dbContext.Database.CurrentTransaction?.RollbackAsync(cancellationToken);
            throw;
        }
    }
}