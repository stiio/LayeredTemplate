using System.Text;
using LayeredTemplate.App.Application.Features.TodoLists.Models;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Application.Features.TodoLists.Handlers;

internal class TodoListFileDownloadHandler : IRequestHandler<TodoListFileDownloadRequest, FileContentResult>
{
    public ValueTask<FileContentResult> Handle(TodoListFileDownloadRequest request, CancellationToken cancellationToken)
    {
        var textFile = "some text";
        return ValueTask.FromResult(new FileContentResult(Encoding.UTF8.GetBytes(textFile), "text/plain")
        {
            FileDownloadName = "text_file.txt",
        });
    }
}