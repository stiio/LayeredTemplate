using FluentValidation;
using LayeredTemplate.Application.Contracts.Requests;

namespace LayeredTemplate.Application.Validators.TodoLists;

public class TodoListCreateValidator : AbstractValidator<TodoListCreateRequest>
{
    public TodoListCreateValidator()
    {
    }
}