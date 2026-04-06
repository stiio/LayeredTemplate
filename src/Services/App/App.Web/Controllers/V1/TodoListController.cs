using LayeredTemplate.App.Application.Features.TodoLists.Models;
using LayeredTemplate.App.Application.Features.TodoLists.Requests;
using LayeredTemplate.App.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Web.Controllers.V1;

[ApiController]
[Route("todo_lists")]
public class TodoListController : AppControllerBase
{
    [HttpPost("search")]
    public ValueTask<TodoListSearchResponse> SearchTodoList(TodoListSearchRequest request)
    {
        return this.Sender.Send(request, this.HttpContext.RequestAborted);
    }

    [HttpPost]
    public ValueTask<TodoListDto> CreateTodoList(TodoListCreateRequest request)
    {
        return this.Sender.Send(request, this.HttpContext.RequestAborted);
    }

    [HttpGet("{id}")]
    public ValueTask<TodoListDto> GetTodoList(TodoListGetRequest request)
    {
        return this.Sender.Send(request, this.HttpContext.RequestAborted);
    }

    [HttpPut("{id}")]
    public ValueTask<TodoListDto> UpdateTodoList(TodoListUpdateRequest request)
    {
        return this.Sender.Send(request, this.HttpContext.RequestAborted);
    }

    [HttpDelete("{id}")]
    public async ValueTask<ActionResult<SuccessfulResult>> DeleteTodoList(TodoListDeleteRequest request)
    {
        await this.Sender.Send(request, this.HttpContext.RequestAborted);
        return this.SuccessfulResult();
    }
}