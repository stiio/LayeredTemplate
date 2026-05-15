using LayeredTemplate.App.Features.TodoLists.Models;

namespace LayeredTemplate.App.Features.TodoLists;

public static class ListTodoListItems
{
    public static void Configure(RouteGroupBuilder group) =>
        group.MapGet("/items", Handle)
            .WithName(nameof(ListTodoListItems))
            .WithSummary("List TodoList items (polymorphic example)");

    public static TodoListItemBase[] Handle() =>
    [
        new TodoListItemOne { Name = "Item 1", DescriptionOne = "Description 1", Subject = "Subject 1" },
        new TodoListItemTwo { Name = "Item 2", DescriptionTwo = "Description 2", Subject = "Subject 2" },
        new TodoListItemThree { Name = "Item 3", DescriptionThree = "Description 3" },
    ];
}
