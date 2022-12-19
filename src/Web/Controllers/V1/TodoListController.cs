using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Common;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Web.Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Controllers.V1;

public class TodoListController : Api.Controllers.V1.TodoListController
{
    private readonly ISender sender;

    public TodoListController(ISender sender)
    {
        this.sender = sender;
    }

    public override async Task<ActionResult<PagedList<TodoListRecordDto>>> SearchTodoList([Required] TodoListSearchRequest request)
    {
        return await this.sender.Send(request);
    }

    public override async Task<ActionResult<TodoListDto>> CreateTodoList([Required] TodoListCreateRequest request)
    {
        return await this.sender.Send(request);
    }

    public override async Task<ActionResult<TodoListDto>> UpdateTodoList([Required] TodoListUpdateRequest request)
    {
        return await this.sender.Send(request);
    }

    public override async Task<ActionResult<TodoListDto>> GetTodoList(Guid todoListId)
    {
        return await this.sender.Send(new TodoListGetRequest(todoListId));
    }

    public override async Task<ActionResult<SuccessfulResult>> DeleteTodoList(Guid todoListId)
    {
        await this.sender.Send(new TodoListDeleteRequest(todoListId));
        return this.Response200();
    }

    public override Task<ActionResult> GetTodoListCsv(Guid todoListId)
    {
        throw new NotImplementedException();
    }
}