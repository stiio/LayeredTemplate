using LayeredTemplate.App.Application.Features.TodoLists.Models;
using Mediator;

namespace LayeredTemplate.App.Application.Features.TodoLists.Requests;

public class TodoListItemListRequest : IRequest<TodoListItemBase[]>
{
}