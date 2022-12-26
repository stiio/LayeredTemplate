using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Web.Api.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V1;

/// <summary>
/// TodoListController
/// </summary>
[ApiController]
[Route("api/v1/todo_lists")]
[Authorize(Roles = $"{Roles.Client}, {Roles.Admin}")]
public class TodoListController : AppControllerBase
{
    private readonly ISender sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoListController"/> class.
    /// </summary>
    /// <param name="sender"></param>
    public TodoListController(ISender sender)
    {
        this.sender = sender;
    }

    /// <summary>
    /// Search TodoList
    /// </summary>
    /// <param name="request">Request body</param>
    /// <returns></returns>
    [HttpPost("search")]
    public async Task<ActionResult<TodoListSearchResponse>> SearchTodoList([Required] TodoListSearchRequest request)
    {
        return await this.sender.Send(request);
    }

    /// <summary>
    /// Create TodoList
    /// </summary>
    /// <param name="request">Request body</param>
    /// <returns>Return <see cref="TodoListDto"/></returns>
    [HttpPost]
    public async Task<ActionResult<TodoListDto>> CreateTodoList([Required] TodoListCreateRequest request)
    {
        return await this.sender.Send(request);
    }

    /// <summary>
    /// Update TodoList
    /// </summary>
    /// <param name="request">Request body</param>
    /// <returns>Return <see cref="TodoListDto"/></returns>
    [HttpPut]
    public async Task<ActionResult<TodoListDto>> UpdateTodoList([Required] TodoListUpdateRequest request)
    {
        return await this.sender.Send(request);
    }

    /// <summary>
    /// Get TodoList by id
    /// </summary>
    /// <param name="todoListId">Id of TodoList</param>
    /// <returns>Return <see cref="TodoListDto"/></returns>
    [HttpGet("{todoListId}")]
    public async Task<ActionResult<TodoListDto>> GetTodoList(Guid todoListId)
    {
        var request = new TodoListGetRequest(todoListId);

        return await this.sender.Send(request);
    }

    /// <summary>
    /// Delete TodoList
    /// </summary>
    /// <param name="todoListId">Id of TodoList</param>
    /// <returns></returns>
    [HttpDelete("{todoListId}")]
    public async Task<ActionResult<SuccessfulResult>> DeleteTodoList(Guid todoListId)
    {
        var request = new TodoListDeleteRequest(todoListId);
        await this.sender.Send(request);

        return this.Response200();
    }

    /// <summary>
    /// Get TodoList Csv
    /// </summary>
    /// <param name="todoListId"></param>
    /// <returns></returns>
    [HttpGet("{todoListId}/csv")]
    [Produces(MediaTypeNames.Application.Octet, Type = typeof(FileResult))]
    [Authorize(Policies.Example)]
    public Task<ActionResult> GetTodoListCsv(Guid todoListId)
    {
        throw new NotImplementedException();
    }
}