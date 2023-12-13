using LayeredTemplate.Application.Features.Users.Services;
using LayeredTemplate.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace LayeredTemplate.Infrastructure.AuthorizationHandlers;

internal class TodoListAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, TodoList>
{
    private readonly ICurrentUserService currentUserService;

    public TodoListAuthorizationHandler(
        ICurrentUserService currentUserService)
    {
        this.currentUserService = currentUserService;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        TodoList resource)
    {
        if (resource.UserId == this.currentUserService.UserId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}