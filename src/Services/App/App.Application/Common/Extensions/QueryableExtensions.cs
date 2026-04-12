using System.Linq.Expressions;
using LayeredTemplate.App.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.App.Application.Common.Extensions;

public static class QueryableExtensions
{
    public static Task<T?> FirstByIdOrDefault<T, TKey>(this IQueryable<T> query, TKey id, CancellationToken cancellationToken = default)
        where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var targetPropertyExpression = Expression.Property(parameter, "Id");
        var sourceValueExpression = Expression.Constant(id);

        var finalExpression = Expression.Equal(targetPropertyExpression, sourceValueExpression);
        var lambda = Expression.Lambda<Func<T, bool>>(finalExpression, parameter);

        return query.FirstOrDefaultAsync(lambda, cancellationToken);
    }

    public static Task<T> FirstById<T, TKey>(this IQueryable<T> query, TKey id, CancellationToken cancellationToken = default)
        where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var targetPropertyExpression = Expression.Property(parameter, "Id");
        var sourceValueExpression = Expression.Constant(id);

        var finalExpression = Expression.Equal(targetPropertyExpression, sourceValueExpression);
        var lambda = Expression.Lambda<Func<T, bool>>(finalExpression, parameter);

        return query.FirstAsync(lambda, cancellationToken);
    }

    public static IQueryable<T> Page<T>(this IQueryable<T> query, PaginationRequest pagination)
    {
        return query
            .Skip((pagination.Page - 1) * pagination.Limit)
            .Take(pagination.Limit);
    }

    public static string PageSql(this string query, PaginationRequest pagination)
    {
        return query += $"\nLIMIT {pagination.Limit} OFFSET {(pagination.Page - 1) * pagination.Limit}";
    }

    public static IQueryable<TEntity> Sort<TEntity, TFields>(this IQueryable<TEntity> query, Sorting<TFields> sorting)
        where TFields : Enum
    {
        var keySelector = CreateKeySelector(typeof(TEntity), sorting.Column.ToString());

        var orderBy = Expression.Call(
            typeof(Queryable),
            sorting.Direction == DirectionType.Asc ? "OrderBy" : "OrderByDescending",
            [typeof(TEntity), keySelector.ReturnType],
            query.Expression,
            Expression.Quote(keySelector));

        return query.Provider.CreateQuery<TEntity>(orderBy);
    }

    public static string SortSql<TFields>(this string query, Sorting<TFields> sorting)
        where TFields : Enum
    {
        var column = "\"" + sorting.Column + "\"";
        return query += $"\nORDER BY {column} {sorting.Direction}";
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
            Total = await query.LongCountAsync(cancellationToken),
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