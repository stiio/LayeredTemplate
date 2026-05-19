using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.App.Shared.Db;

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
}