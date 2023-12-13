using FluentValidation;
using LayeredTemplate.Application.TodoLists.Models;
using LayeredTemplate.Application.TodoLists.Requests;

namespace LayeredTemplate.Application.TodoLists.Validators;

internal class TodoListCreateRequestValidator : AbstractValidator<TodoListCreateRequest>
{
    public TodoListCreateRequestValidator(IValidator<TodoListCreateRequestBody> bodyValidator)
    {
        this.RuleFor(x => x.Body)
            .SetValidator(bodyValidator);
    }
}