using System.Linq.Expressions;
using FluentValidation;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Domain.Common;
using LayeredTemplate.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Application.Common.Extensions;

public static class RuleBuilderExtensions
{
    public static IRuleBuilderOptions<TRequest, Guid> ExistsEntity<TRequest, TEntity>(
        this IRuleBuilder<TRequest, Guid> ruleBuilder, IApplicationDbContext dbContext)
        where TEntity : class, IBaseEntity
    {
        return ruleBuilder.MustAsync(async (request, entityId, context, stopToken) =>
            {
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

    public static IRuleBuilderOptions<TRequest, Guid?> ExistsEntity<TRequest, TEntity>(
        this IRuleBuilder<TRequest, Guid?> ruleBuilder, IApplicationDbContext dbContext)
        where TEntity : class, IBaseEntity
    {
        return ruleBuilder.MustAsync(async (request, entityId, context, stopToken) =>
            {
                if (entityId is null)
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

    public static IRuleBuilderOptions<TRequest, Guid?> RequireAccess<TRequest, TEntity>(
        this IRuleBuilder<TRequest, Guid?> ruleBuilder,
        RequirementAction requirementAction,
        IApplicationDbContext dbContext,
        IRequirementAuthorizationService requirementAuthorizationService)
        where TEntity : class, IBaseEntity
    {
        return ruleBuilder.MustAsync(async (request, entityId, context, stopToken) =>
            {
                if (entityId is null)
                {
                    return true;
                }

                var authorizationService = await requirementAuthorizationService.Authorize<TEntity>(
                    entityId.Value,
                    requirementAction);

                return authorizationService.Succeeded;
            })
            .WithMessage("Access denied.");
    }

    public static IRuleBuilderOptions<TRequest, Guid> RequireAccess<TRequest, TEntity>(
        this IRuleBuilder<TRequest, Guid> ruleBuilder,
        RequirementAction requirementAction,
        IApplicationDbContext dbContext,
        IRequirementAuthorizationService requirementAuthorizationService)
        where TEntity : class, IBaseEntity
    {
        return ruleBuilder.MustAsync(async (request, entityId, context, stopToken) =>
            {
                var authorizationService = await requirementAuthorizationService.Authorize<TEntity>(
                    entityId,
                    requirementAction);

                return authorizationService.Succeeded;
            })
            .WithMessage("Access denied.");
    }
}