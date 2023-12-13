using FluentValidation;
using LayeredTemplate.Application.TodoLists.Models;

namespace LayeredTemplate.Application.TodoLists.Validators;

public class TodoListCreateRequestBodyValidator : AbstractValidator<TodoListCreateRequestBody>
{
    public TodoListCreateRequestBodyValidator()
    {
        this.RuleFor(x => x.Name)
            .NotNull()
            .MaximumLength(255);
    }
}