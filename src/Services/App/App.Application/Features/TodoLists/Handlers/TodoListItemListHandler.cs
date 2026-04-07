using LayeredTemplate.App.Application.Features.TodoLists.Models;
using LayeredTemplate.App.Application.Features.TodoLists.Requests;
using Mediator;

namespace LayeredTemplate.App.Application.Features.TodoLists.Handlers;

internal class TodoListItemListHandler : IRequestHandler<TodoListItemListRequest, TodoListItemBase[]>
{
    public ValueTask<TodoListItemBase[]> Handle(TodoListItemListRequest request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<TodoListItemBase[]>([
            new TodoListItemOne()
            {
                Name = "Item 1",
                DescriptionOne = "Description 1",
                Subject = "Subject 1",
            },
            new TodoListItemTwo()
            {
                Name = "Item 2",
                DescriptionTwo = "Description 2",
                Subject = "Subject 2",
            },
            new TodoListItemThree()
            {
                Name = "Item 3",
                DescriptionThree = "Description 3",
            }
        ]);
    }
}