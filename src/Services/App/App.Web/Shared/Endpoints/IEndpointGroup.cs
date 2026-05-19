namespace LayeredTemplate.App.Shared.Endpoints;

/// <summary>
/// Declares a route group (prefix + shared metadata like tags, OpenAPI version, authorization).
/// Endpoints opt into a group via <see cref="EndpointGroupAttribute{TGroup}"/>; discovery
/// materializes each <see cref="IEndpointGroup"/> implementation exactly once and reuses the
/// resulting <see cref="RouteGroupBuilder"/> for every endpoint that targets it.
/// </summary>
/// <remarks>
/// <para>One feature can declare multiple groups when its surface area splits across different
/// audiences — e.g. <c>TodoListsGroup</c> at <c>/api/v1/todo_lists</c> for end users plus
/// <c>TodoListsAdminGroup</c> at <c>/api/v1/admin/todo_lists</c> with a tighter auth policy.</para>
/// <para>Endpoints with no <see cref="EndpointGroupAttribute{TGroup}"/> register against the root
/// <see cref="IEndpointRouteBuilder"/> directly — useful for ungrouped one-offs like
/// <c>/health</c> or a fully self-contained info endpoint.</para>
/// </remarks>
public interface IEndpointGroup
{
    static abstract RouteGroupBuilder MapGroup(IEndpointRouteBuilder app);
}
