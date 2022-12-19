using LayeredTemplate.Application.Contracts.Common;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Domain.Enums;
using LayeredTemplate.Web.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Mock.Controllers.V1;

public class TodoListController : Api.Controllers.V1.TodoListController
{
    public override Task<ActionResult<PagedList<TodoListRecordDto>>> SearchTodoList(TodoListSearchRequest request)
    {
        return Task.FromResult<ActionResult<PagedList<TodoListRecordDto>>>(
            new PagedList<TodoListRecordDto>()
            {
                Pagination = new Pagination()
                {
                    Page = 1,
                    Limit = 10,
                    Total = 2,
                },
                Data = new TodoListRecordDto[]
                {
                    new TodoListRecordDto()
                    {
                        Id = new Guid("3F9B1991-D2E6-42FD-832B-1F8B39EDAA25"),
                        UserId = new Guid("F98E3325-86D7-42DE-BFB6-A9D9F034710F"),
                        Name = "Name 1",
                        Type = TodoListType.Default,
                        CreatedAt = DateTime.UtcNow,
                    },
                    new TodoListRecordDto()
                    {
                        Id = new Guid("CCC00352-5928-4881-A9D2-067ED02C102A"),
                        UserId = new Guid("F98E3325-86D7-42DE-BFB6-A9D9F034710F"),
                        Name = "Name 1",
                        Type = TodoListType.Default,
                        CreatedAt = DateTime.UtcNow,
                    },
                },
            });
    }

    public override Task<ActionResult<TodoListDto>> CreateTodoList(TodoListCreateRequest request)
    {
        throw new NotImplementedException();
    }

    public override Task<ActionResult<TodoListDto>> UpdateTodoList(TodoListUpdateRequest request)
    {
        throw new NotImplementedException();
    }

    public override Task<ActionResult<TodoListDto>> GetTodoList(Guid todoListId)
    {
        throw new NotImplementedException();
    }

    public override Task<ActionResult<SuccessfulResult>> DeleteTodoList(Guid todoListId)
    {
        throw new NotImplementedException();
    }

    public override Task<ActionResult> GetTodoListCsv(Guid todoListId)
    {
        throw new NotImplementedException();
    }
}