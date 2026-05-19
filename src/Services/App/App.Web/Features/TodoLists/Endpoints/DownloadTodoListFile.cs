using System.Text;
using LayeredTemplate.App.Shared.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LayeredTemplate.App.Features.TodoLists.Endpoints;

[EndpointGroup<TodoListsGroup>]
public sealed class DownloadTodoListFile : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/file", Handle)
            .WithName(nameof(DownloadTodoListFile))
            .WithSummary("Download TodoList file (example)")
            .Produces<FileContentHttpResult>(200, "application/octet-stream");

    public static FileContentHttpResult Handle() =>
        TypedResults.File(
            Encoding.UTF8.GetBytes("some text"),
            contentType: "text/plain",
            fileDownloadName: "text_file.txt");
}
