using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Common;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Domain.Enums;
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

    public override Task<ActionResult<PagedList<TodoListRecordDto>>> SearchTodoList([Required] TodoListSearchRequest request)
    {
        // TODO: Mock
        // return await this.sender.Send(request);
        return Task.FromResult<ActionResult<PagedList<TodoListRecordDto>>>(new PagedList<TodoListRecordDto>()
        {
            Pagination = new Pagination()
            {
                Page = 1,
                Limit = 10,
                Total = 0,
            },
            Data = Array.Empty<TodoListRecordDto>(),
        });
    }

    public override Task<ActionResult<TodoListDto>> CreateTodoList([Required] TodoListCreateRequest request)
    {
        // TODO: Mock
        // return await this.sender.Send(request);
        return Task.FromResult<ActionResult<TodoListDto>>(new TodoListDto()
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
        });
    }

    public override Task<ActionResult<TodoListDto>> GetTodoList(Guid todoListId)
    {
        // TODO: Mock
        // return await this.sender.Send(new TodoListGetRequest(todoListId));
        return Task.FromResult<ActionResult<TodoListDto>>(new TodoListDto()
        {
            Id = todoListId,
            Name = "Name",
            Type = TodoListType.Default,
        });
    }

    public override Task<ActionResult<TodoListDto>> UpdateTodoList(Guid todoListId, [Required] TodoListUpdateRequest request)
    {
        if (todoListId != request.Id)
        {
            return Task.FromResult<ActionResult<TodoListDto>>(this.NotEqualIdsResponse());
        }

        // TODO: Mock
        // return await this.sender.Send(request);
        return Task.FromResult<ActionResult<TodoListDto>>(new TodoListDto()
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
        });
    }

    public override Task<ActionResult<SuccessfulResult>> DeleteTodoList(Guid todoListId)
    {
        // TODO: Mock
        // await this.sender.Send(new TodoListDeleteRequest(todoListId));
        return Task.FromResult<ActionResult<SuccessfulResult>>(this.Response200());
    }

    public override Task<ActionResult> GetTodoListCsv(Guid todoListId)
    {
        throw new NotImplementedException();
    }
}