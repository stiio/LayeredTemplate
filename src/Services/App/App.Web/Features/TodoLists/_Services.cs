using LayeredTemplate.App.Features.TodoLists.Services;
using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features.TodoLists;

public class TodoListsServices : IFeatureServices
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITodoListRatingService, TodoListRatingService>();
    }
}