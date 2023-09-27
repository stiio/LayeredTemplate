using FluentValidation;
using LayeredTemplate.Application.Contracts.Models.TodoLists;
using LayeredTemplate.Application.Contracts.Requests.TodoLists;

namespace LayeredTemplate.Application.Validators.TodoLists;

internal class TodoListCreateRequestValidator : AbstractValidator<TodoListCreateRequest>
{
    public TodoListCreateRequestValidator(IValidator<TodoListCreateRequestBody> bodyValidator)
    {
        this.RuleFor(x => x.Body)
            .SetValidator(bodyValidator);
    }
}