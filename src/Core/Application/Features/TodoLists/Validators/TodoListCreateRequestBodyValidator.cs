using FluentValidation;
using LayeredTemplate.Application.Features.TodoLists.Models;

namespace LayeredTemplate.Application.Features.TodoLists.Validators;

public class TodoListCreateRequestBodyValidator : AbstractValidator<TodoListCreateRequestBody>
{
    public TodoListCreateRequestBodyValidator()
    {
        this.RuleFor(x => x.Name)
            .NotNull()
            .MaximumLength(255);
    }
}