using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using LayeredTemplate.Application.Contracts.Common;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Web.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V1;

/// <summary>
/// TodoListController
/// </summary>
[ApiController]
[Route("api/v1/todo_lists")]
[Authorize(Roles = $"{Roles.Client}, {Roles.Admin}")]
public abstract class TodoListController : AppControllerBase
{
    /// <summary>
    /// Search TodoList
    /// </summary>
    /// <param name="request">Request body</param>
    /// <returns></returns>
    [HttpPost("search")]
    public abstract Task<ActionResult<PagedList<TodoListRecordDto>>> SearchTodoList([Required] TodoListSearchRequest request);

    /// <summary>
    /// Create TodoList
    /// </summary>
    /// <param name="request">Request body</param>
    /// <returns>Return <see cref="TodoListDto"/></returns>
    [HttpPost]
    public abstract Task<ActionResult<TodoListDto>> CreateTodoList([Required] TodoListCreateRequest request);

    /// <summary>
    /// Get TodoList by id
    /// </summary>
    /// <param name="todoListId">Id of TodoList</param>
    /// <returns>Return <see cref="TodoListDto"/></returns>
    [HttpGet("{todoListId}")]
    public abstract Task<ActionResult<TodoListDto>> GetTodoList(Guid todoListId);

    /// <summary>
    /// Update TodoList
    /// </summary>
    /// <param name="todoListId">Id of TodoList</param>
    /// <param name="request">Request body</param>
    /// <returns>Return <see cref="TodoListDto"/></returns>
    [HttpPut("{todoListId}")]
    public abstract Task<ActionResult<TodoListDto>> UpdateTodoList(Guid todoListId, [Required] TodoListUpdateRequest request);

    /// <summary>
    /// Delete TodoList
    /// </summary>
    /// <param name="todoListId">Id of TodoList</param>
    /// <returns></returns>
    [HttpDelete("{todoListId}")]
    public abstract Task<ActionResult<SuccessfulResult>> DeleteTodoList(Guid todoListId);

    /// <summary>
    /// Get TodoList Csv
    /// </summary>
    /// <param name="todoListId"></param>
    /// <returns></returns>
    [HttpGet("{todoListId}/csv")]
    [Produces(MediaTypeNames.Application.Octet, Type = typeof(FileResult))]
    [Authorize(Policies.Example)]
    public abstract Task<ActionResult> GetTodoListCsv(Guid todoListId);
}