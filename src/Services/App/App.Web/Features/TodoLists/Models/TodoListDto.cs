using System.Text.Json.Serialization;

namespace LayeredTemplate.App.Features.TodoLists.Models;

public sealed class TodoListDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = null!;

    public string? Description { get; init; }

    public TodoListType Type { get; init; }

    public DateTime CreatedAt { get; init; }
}

public enum TodoListType
{
    Type1,
    Type2,
}

public enum TodoListFields
{
    Id,
    Name,
    CreatedAt,
}

public sealed class TodoListSearchFilterDto
{
    public string? Search { get; set; }
}

[JsonDerivedType(typeof(TodoListItemOne), typeDiscriminator: "One")]
[JsonDerivedType(typeof(TodoListItemTwo), typeDiscriminator: "Two")]
[JsonDerivedType(typeof(TodoListItemThree), typeDiscriminator: "Three")]
public abstract class TodoListItemBase
{
    public string Name { get; set; } = null!;
}

public sealed class TodoListItemOne : TodoListItemBase
{
    public string? DescriptionOne { get; set; }

    public string? Subject { get; set; }
}

public sealed class TodoListItemTwo : TodoListItemBase
{
    public string? DescriptionTwo { get; set; }

    public string? Subject { get; set; }
}

public sealed class TodoListItemThree : TodoListItemBase
{
    public string? DescriptionThree { get; set; }
}
