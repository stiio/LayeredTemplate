using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LayeredTemplate.App.Features.TodoLists;

public static class DownloadTodoListFile
{
    public static void Configure(RouteGroupBuilder group) =>
        group.MapGet("/file", Handle)
            .WithName(nameof(DownloadTodoListFile))
            .WithSummary("Download TodoList file (example)")
            .Produces<FileContentHttpResult>(200, "application/octet-stream");

    public static FileContentHttpResult Handle() =>
        TypedResults.File(
            Encoding.UTF8.GetBytes("some text"),
            contentType: "text/plain",
            fileDownloadName: "text_file.txt");
}
