using System.Linq.Expressions;
using LayeredTemplate.Application.Contracts.Enums;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Application.Common.ExtensionsQueryable;

internal static class QueryableExtensions
{
    public static Task<T?> SelectForUpdate<T, TKey>(this DbSet<T> dbSet, TKey id)
        where T : class, IBaseEntity<TKey>
    {
        var tableName = dbSet.EntityType.GetTableName();
        return dbSet.FromSqlRaw(@$"
                SELECT * FROM {tableName}
                WHERE id = '{id}'
                FOR UPDATE")
            .Where(entity => entity.Id!.Equals(id))
            .FirstOrDefaultAsync();
    }

    public static IQueryable<T> Page<T>(this IQueryable<T> query, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        return query
            .Skip((pagination.Page - 1) * pagination.Limit)
            .Take(pagination.Limit);
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

    public static async Task<PaginationResponse> ToPaginationResponse<T>(
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