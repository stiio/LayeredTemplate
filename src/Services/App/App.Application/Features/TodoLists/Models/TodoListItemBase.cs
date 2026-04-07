using System.Text.Json.Serialization;

namespace LayeredTemplate.App.Application.Features.TodoLists.Models;

[JsonDerivedType(typeof(TodoListItemOne), typeDiscriminator: "One")]
[JsonDerivedType(typeof(TodoListItemTwo), typeDiscriminator: "Two")]
[JsonDerivedType(typeof(TodoListItemThree), typeDiscriminator: "Three")]
public abstract class TodoListItemBase
{
    public string Name { get; set; } = null!;
}