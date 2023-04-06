using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Infrastructure.Data.Services;

internal class ApplicationDbContextFactory : IApplicationDbContextFactory
{
    private readonly IDbContextFactory<ApplicationDbContext> contextFactory;

    public ApplicationDbContextFactory(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        this.contextFactory = contextFactory;
    }

    public async Task<IApplicationDbContext> CreateAsync(CancellationToken cancellationToken = default)
    {
        return await this.contextFactory.CreateDbContextAsync(cancellationToken);
    }
}