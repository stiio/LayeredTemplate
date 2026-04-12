using FluentValidation;

namespace LayeredTemplate.App.Application.Features.TodoLists.Requests;

internal class TodoListCreateRequestValidator : AbstractValidator<TodoListCreateRequest>
{
    public TodoListCreateRequestValidator()
    {
        this.RuleFor(x => x.Body)
            .ChildRules(body =>
            {
                body.RuleFor(x => x.Name)
                    .NotEmpty()
                    .MinimumLength(3);
            });
    }
}