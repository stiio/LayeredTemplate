using FluentValidation;
using LayeredTemplate.Application.Features.TodoLists.Models;
using LayeredTemplate.Application.Features.TodoLists.Requests;

namespace LayeredTemplate.Application.Features.TodoLists.Validators;

internal class TodoListCreateRequestValidator : AbstractValidator<TodoListCreateRequest>
{
    public TodoListCreateRequestValidator(IValidator<TodoListCreateRequestBody> bodyValidator)
    {
        this.RuleFor(x => x.Body)
            .SetValidator(bodyValidator);
    }
}