using FluentValidation;
using LayeredTemplate.Application.Contracts.Requests;

namespace LayeredTemplate.Application.Handlers.TodoLists.TodoListCreate;

public class TodoListCreateValidator : AbstractValidator<TodoListCreateRequest>
{
    public TodoListCreateValidator()
    {
        this.RuleFor(x => x.Name)
            .MaximumLength(25);
    }
}