using LayeredTemplate.App.Application.Common.Models;
using LayeredTemplate.App.Application.Features.TodoLists.Models;
using LayeredTemplate.App.Application.Features.TodoLists.Requests;
using Mediator;

namespace LayeredTemplate.App.Application.Features.TodoLists.Handlers;

internal class TodoListSearchHandler : IRequestHandler<TodoListSearchRequest, TodoListSearchResponse>
{
    public ValueTask<TodoListSearchResponse> Handle(TodoListSearchRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<TodoListSearchResponse>(new TodoListSearchResponse()
        {
            Pagination = new PaginationResponse()
            {
                Page = request.Body.Pagination.Page,
                Limit = request.Body.Pagination.Limit,
                Total = 3,
            },
            Filter = request.Body.Filter,
            Sorting = request.Body.Sorting,
            Data =
            [
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "List 1",
                    Description = "List description 1",
                    CreatedAt = DateTime.UtcNow,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "List 2",
                    Description = "List description 2",
                    CreatedAt = DateTime.UtcNow,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "List 3",
                    Description = "List description 3",
                    CreatedAt = DateTime.UtcNow,
                }
            ],
        });
    }
}