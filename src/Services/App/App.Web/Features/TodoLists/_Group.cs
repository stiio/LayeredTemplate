using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features.TodoLists;

/// <summary>
/// Route group + DI registration for the TodoLists feature. Endpoints opt into this group via
/// <c>[EndpointGroup&lt;TodoListsGroup&gt;]</c>; discovery materialises the group once and
/// dispatches each endpoint's <c>Map</c> against the resulting <see cref="RouteGroupBuilder"/>.
/// </summary>
public sealed class TodoListsGroup : IEndpointGroup
{
    public static RouteGroupBuilder MapGroup(IEndpointRouteBuilder app) =>
        app.MapGroup("/api/v1/todo_lists")
            .WithTags("TodoLists")
            .WithGroupName("v1");
}
