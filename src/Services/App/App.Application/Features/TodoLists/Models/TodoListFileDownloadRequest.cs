using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Application.Features.TodoLists.Models;

public class TodoListFileDownloadRequest : IRequest<FileContentResult>
{
}