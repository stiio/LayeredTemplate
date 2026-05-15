using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features.TodoLists;

public sealed class TodoListsRoutes : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1/todo_lists")
            .WithTags("TodoLists")
            .WithGroupName("v1");

        SearchTodoLists.Configure(v1);
        CreateTodoList.Configure(v1);
        GetTodoList.Configure(v1);
        UpdateTodoList.Configure(v1);
        DeleteTodoList.Configure(v1);
        ListTodoListItems.Configure(v1);
        CreateTodoListItems.Configure(v1);
    }
}
