using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.App.Application.Common.Models;

#pragma warning disable SA1402 // File may only contain a single type

public class Sorting<TFields>
    where TFields : Enum
{
    /// <summary>Name of field for sort</summary>
    [Required]
    public TFields Column { get; set; } = default!;

    [Required]
    public DirectionType Direction { get; set; } = DirectionType.Desc;
}