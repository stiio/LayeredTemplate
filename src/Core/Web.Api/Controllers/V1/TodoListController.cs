using System.Net.Mime;
using LayeredTemplate.Application.Contracts.Models.TodoLists;
using LayeredTemplate.Application.Contracts.Requests.TodoLists;
using LayeredTemplate.Domain.Enums;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Web.Api.Attributes;
using LayeredTemplate.Web.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V1;

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