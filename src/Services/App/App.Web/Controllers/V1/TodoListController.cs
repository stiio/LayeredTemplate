using LayeredTemplate.App.Application.Features.TodoLists.Models;
using LayeredTemplate.App.Application.Features.TodoLists.Requests;
using LayeredTemplate.App.Web.Attributes;
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

    /// <summary>
    /// Create TodoList
    /// </summary>
    /// <remarks>
    /// This is remarks
    /// </remarks>
    /// <param name="request">some request description</param>
    /// <example>{ "name": "some name", "description": "some description" }</example>
    /// <returns>Response description</returns>
    [HttpPost]
    public ValueTask<TodoListDto> CreateTodoList(TodoListCreateRequest request)
    {
        return this.Sender.Send(request, this.HttpContext.RequestAborted);
    }

    [HttpPost("file")]
    public ValueTask<TodoListDto> CreateTodoListFile(TodoListFileCreateRequest request)
    {
        return this.Sender.Send(request, this.HttpContext.RequestAborted);
    }

    [FileResponse]
    [HttpGet("file")]
    public async ValueTask<ActionResult> DownloadTodoListFile()
    {
        return await this.Sender.Send(new TodoListFileDownloadRequest());
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

    [HttpGet("items")]
    public virtual ValueTask<TodoListItemBase[]> ListTodoListItems()
    {
        return this.Sender.Send(new TodoListItemListRequest(),  this.HttpContext.RequestAborted);
    }

    [HttpPost("items")]
    public virtual ValueTask<TodoListItemBase[]> CreateTodoListItems(TodoListItemsCreateRequest request)
    {
        return this.Sender.Send(request, this.HttpContext.RequestAborted);
    }
}