using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.App.Shared.Pagination;

public static class QueryableExtensions
{
    public static IQueryable<T> Page<T>(this IQueryable<T> query, PaginationRequest pagination) =>
        query.Skip((pagination.Page - 1) * pagination.Limit).Take(pagination.Limit);

    public static string PageSql(this string query, PaginationRequest pagination) =>
        query + $"\nLIMIT {pagination.Limit} OFFSET {(pagination.Page - 1) * pagination.Limit}";

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
        return query + $"\nORDER BY {column} {sorting.Direction}";
    }

    public static async Task<PaginationResponse> ToPaginationResponse<T>(
        this IQueryable<T> query,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default) =>
        new()
        {
            Page = pagination.Page,
            Limit = pagination.Limit,
            Total = await query.LongCountAsync(cancellationToken),
        };

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
