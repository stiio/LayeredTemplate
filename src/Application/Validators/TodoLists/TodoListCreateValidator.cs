using FluentValidation;
using LayeredTemplate.Application.Contracts.Requests;

namespace LayeredTemplate.Application.Validators.TodoLists;

public class TodoListCreateValidator : AbstractValidator<TodoListCreateRequest>
{
    public TodoListCreateValidator()
    {
        this.RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(15);

        this.RuleFor(x => x.Type)
            .NotNull();
    }
}