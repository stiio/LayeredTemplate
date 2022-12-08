using System.Linq.Expressions;
using LayeredTemplate.Application.Contracts.Common;
using LayeredTemplate.Application.Contracts.Enums;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Application.Common.QueryableExtensions;

internal static class QueryableExtensions
{
    public static async Task<PagedList<T>> ToPagedList<T>(this IQueryable<T> query, Pagination? pagination, CancellationToken cancellationToken = default)
    {
        var page = pagination?.Page ?? 1;
        var limit = pagination?.Limit ?? 10;

        return new PagedList<T>()
        {
            Pagination = new Pagination()
            {
                Page = page,
                Limit = limit,
                Total = await query.CountAsync(cancellationToken),
            },
            Data = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToArrayAsync(cancellationToken),
        };
    }

    public static IQueryable<T> ApplySorting<T>(this IQueryable<T> query, Sorting sorting)
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
}