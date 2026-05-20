namespace LayeredTemplate.App.Shared.Pagination;

public sealed class Sorting<TFields>
    where TFields : Enum
{
    public TFields Column { get; set; } = default!;

    public DirectionType Direction { get; set; } = DirectionType.Desc;
}