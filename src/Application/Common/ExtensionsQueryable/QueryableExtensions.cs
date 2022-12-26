using System.Linq.Expressions;
using LayeredTemplate.Application.Contracts.Enums;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Application.Common.ExtensionsQueryable;

internal static class QueryableExtensions
{
    public static Task<T?> SelectForUpdate<T>(this DbSet<T> dbSet, Guid id)
        where T : class, IBaseEntity<Guid>
    {
        var tableName = dbSet.EntityType.GetTableName();
        return dbSet.FromSqlRaw(@$"
                SELECT * FROM ""{tableName}""
                WHERE ""Id"" = '{id}'
                FOR UPDATE")
            .Where(entity => entity.Id == id)
            .FirstOrDefaultAsync();
    }

    public static async Task<T[]> Page<T>(this IQueryable<T> query, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        return await query
            .Skip((pagination.Page - 1) * pagination.Limit)
            .Take(pagination.Limit)
            .ToArrayAsync(cancellationToken);
    }

    public static IQueryable<T> Sort<T>(this IQueryable<T> query, Sorting sorting)
    {
        var queryElementTypeParam = Expression.Parameter(typeof(T));
        var memberAccess = Expression.PropertyOrField(queryElementTypeParam, sorting.Column);
        var keySelector = Expression.Lambda(memberAccess, queryElementTypeParam);

        var orderBy = Expression.Call(
            typeof(Queryable),
            sorting.Direction == DirectionType.Asc ? "OrderBy" : "OrderByDescending",
            new Type[] { typeof(T), memberAccess.Type },
            query.Expression,
            Expression.Quote(keySelector));

        return query.Provider.CreateQuery<T>(orderBy);
    }

    public static async Task<PaginationResponse> PaginationResponse<T>(
        this IQueryable<T> query,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        return new PaginationResponse()
        {
            Page = pagination.Page,
            Limit = pagination.Limit,
            Total = await query.CountAsync(cancellationToken),
        };
    }
}