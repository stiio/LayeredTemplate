using System.Net.Mime;
using LayeredTemplate.Application.Features.TodoLists.Models;
using LayeredTemplate.Application.Features.TodoLists.Requests;
using LayeredTemplate.Domain.Enums;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Web.Attributes;
using LayeredTemplate.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Controllers.V1;

[ApiController]
[Route("todo_lists")]
[RoleAuthorize(Role.Client, Role.Admin)]
public class TodoListController : AppControllerBase
{
    [HttpPost("search")]
    public Task<TodoListSearchResponse> SearchTodoList(TodoListSearchRequest request)
    {
        return this.Sender.Send(request);
    }

    [HttpPost]
    public Task<TodoListDto> CreateTodoList(TodoListCreateRequest request)
    {
        return this.Sender.Send(request);
    }

    [HttpPut("{id}")]
    public Task<TodoListDto> UpdateTodoList(TodoListUpdateRequest request)
    {
        return this.Sender.Send(request);
    }

    [HttpGet("{id}")]
    public Task<TodoListDto> GetTodoList(TodoListGetRequest request)
    {
        return this.Sender.Send(request);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<SuccessfulResult>> DeleteTodoList(TodoListDeleteRequest request)
    {
        await this.Sender.Send(request);

        return this.SuccessfulResult();
    }

    [HttpGet("{id}/csv")]
    [Produces(MediaTypeNames.Application.Octet, Type = typeof(FileResult))]
    [Authorize(Policies.Example)]
    public Task<ActionResult> GetTodoListCsv(TodoListCsvGetRequest request)
    {
        throw new NotImplementedException();
    }
}