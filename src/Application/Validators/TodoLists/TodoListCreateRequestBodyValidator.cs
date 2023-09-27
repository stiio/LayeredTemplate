using FluentValidation;
using LayeredTemplate.Application.Contracts.Models.TodoLists;

namespace LayeredTemplate.Application.Validators.TodoLists;

public class TodoListCreateRequestBodyValidator : AbstractValidator<TodoListCreateRequestBody>
{
    public TodoListCreateRequestBodyValidator()
    {
        this.RuleFor(x => x.Name)
            .NotNull()
            .MaximumLength(255);
    }
}