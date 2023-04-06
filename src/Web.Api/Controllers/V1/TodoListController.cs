using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Domain.Enums;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Web.Api.Attributes;
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
[RoleAuthorize(Role.Client, Role.Admin)]
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
    public async Task<ActionResult<TodoListSearchResponse>> SearchTodoList([FromRoute] TodoListSearchRequest request)
    {
        return await this.sender.Send(request);
    }

    /// <summary>
    /// Create TodoList
    /// </summary>
    /// <param name="request">Request body</param>
    /// <returns>Return <see cref="TodoListDto"/></returns>
    [HttpPost]
    public async Task<ActionResult<TodoListDto>> CreateTodoList([FromRoute] TodoListCreateRequest request)
    {
        return await this.sender.Send(request);
    }

    /// <summary>
    /// Update TodoList
    /// </summary>
    /// <param name="request">Request body</param>
    /// <returns>Return <see cref="TodoListDto"/></returns>
    [HttpPut("{id}")]
    public async Task<ActionResult<TodoListDto>> UpdateTodoList([FromRoute] TodoListUpdateRequest request)
    {
        return await this.sender.Send(request);
    }

    /// <summary>
    /// Get TodoList by id
    /// </summary>
    /// <param name="request"></param>
    /// <returns>Return <see cref="TodoListDto"/></returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<TodoListDto>> GetTodoList([FromRoute] TodoListGetRequest request)
    {
        return await this.sender.Send(request);
    }

    /// <summary>
    /// Delete TodoList
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public async Task<ActionResult<SuccessfulResult>> DeleteTodoList([FromRoute] TodoListDeleteRequest request)
    {
        await this.sender.Send(request);

        return this.SuccessfulResult();
    }

    /// <summary>
    /// Get TodoList Csv
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpGet("{id}/csv")]
    [Produces(MediaTypeNames.Application.Octet, Type = typeof(FileResult))]
    [Authorize(Policies.Example)]
    public Task<ActionResult> GetTodoListCsv([FromRoute] TodoListCsvGetRequest request)
    {
        throw new NotImplementedException();
    }
}