using LayeredTemplate.App.Features.TodoLists.Services;
using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features.TodoLists;

public sealed class TodoListsRoutes : IEndpoint, IFeatureServices
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITodoListRatingService, TodoListRatingService>();
    }

    public static void Map(IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1/todo_lists")
            .WithTags("TodoLists")
            .WithGroupName("v1");

        SearchTodoLists.Configure(v1);
        CreateTodoList.Configure(v1);
        CreateTodoListFile.Configure(v1);
        DownloadTodoListFile.Configure(v1);
        GetTodoList.Configure(v1);
        UpdateTodoList.Configure(v1);
        DeleteTodoList.Configure(v1);
        ListTodoListItems.Configure(v1);
        CreateTodoListItems.Configure(v1);
        RateTodoList.Configure(v1);
    }
}
