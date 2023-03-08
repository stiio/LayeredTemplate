using AutoBogus;
using LayeredTemplate.Application.Contracts.Models;

namespace LayeredTemplate.Web.Api.Bogus;

internal sealed class TodoListDtoFaker : AutoFaker<TodoListDto>
{
    public TodoListDtoFaker()
    {
        this.RuleFor(x => x.Name, f => f.Lorem.Word());
    }
}