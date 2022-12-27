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
        var keySelector = CreateKeySelector(typeof(T), sorting.Column);

        var orderBy = Expression.Call(
            typeof(Queryable),
            sorting.Direction == DirectionType.Asc ? "OrderBy" : "OrderByDescending",
            new Type[] { typeof(T), keySelector.ReturnType },
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

    private static LambdaExpression CreateKeySelector(Type type, string propertyName)
    {
        var param = Expression.Parameter(type);
        Expression body = param;
        foreach (var member in propertyName.Split('.'))
        {
            body = Expression.PropertyOrField(body, member);
        }

        return Expression.Lambda(body, param);
    }
}