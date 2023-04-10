using System.Linq.Expressions;
using FluentValidation;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.QueryableExtensions;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using PhoneNumbers;

namespace LayeredTemplate.Application.Common.Extensions;

public static class RuleBuilderExtensions
{
    public static IRuleBuilderOptions<T, TKey> ExistsEntity<T, TKey, TEntity>(
        this IRuleBuilder<T, TKey> ruleBuilder, IApplicationDbContext dbContext)
        where TEntity : class
    {
        return ruleBuilder.MustAsync(async (request, entityId, context, stopToken) =>
            {
                if (entityId == null)
                {
                    return true;
                }

                context.MessageFormatter.AppendArgument("entityName", typeof(TEntity).Name);
                context.MessageFormatter.AppendArgument("id", entityId);

                var parameter = Expression.Parameter(typeof(TEntity), "x");
                var targetPropertyExpression = Expression.Property(parameter, "Id");
                var sourceValueExpression = Expression.Constant(entityId);

                var finalExpression = Expression.Equal(targetPropertyExpression, sourceValueExpression);
                var lambda = Expression.Lambda<Func<TEntity, bool>>(finalExpression, parameter);

                return await dbContext.Set<TEntity>().AnyAsync(lambda, stopToken);
            })
            .WithMessage("Entity '{entityName}' ({id}) was not found.");
    }

    public static IRuleBuilderOptions<T, TKey> RequireAccess<T, TKey, TEntity>(
        this IRuleBuilder<T, TKey> ruleBuilder,
        OperationAuthorizationRequirement requirement,
        IApplicationDbContext dbContext,
        IResourceAuthorizationService resourceAuthorizationService)
        where TEntity : class
    {
        return ruleBuilder.MustAsync(async (request, entityId, context, stopToken) =>
            {
                if (entityId is null)
                {
                    return true;
                }

                var entity = await dbContext.Set<TEntity>().FindByIdOrDefault(entityId, stopToken);
                if (entity is null)
                {
                    return true;
                }

                var authorizationService = await resourceAuthorizationService.Authorize(entity, requirement);
                return authorizationService.Succeeded;
            })
            .WithMessage("Access denied.");
    }
}