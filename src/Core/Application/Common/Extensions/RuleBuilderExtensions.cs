using System.Linq.Expressions;
using FluentValidation;
using LayeredTemplate.Application.Common.Services;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Application.Common.Extensions;

public static class RuleBuilderExtensions
{
    public static IRuleBuilderOptions<TRequest, TProperty> ExistsEntity<TRequest, TProperty, TEntity>(
        this IRuleBuilder<TRequest, TProperty> ruleBuilder, IApplicationDbContext dbContext)
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

    public static IRuleBuilderOptions<TRequest, TProperty> RequireAccess<TRequest, TProperty, TEntity>(
        this IRuleBuilder<TRequest, TProperty> ruleBuilder,
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

                var entity = await dbContext.Set<TEntity>().FirstByIdOrDefault(entityId, stopToken);
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