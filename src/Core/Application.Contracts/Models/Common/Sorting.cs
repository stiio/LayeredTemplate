using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Application.Contracts.Models.Common;

#pragma warning disable SA1402 // File may only contain a single type

public class Sorting
{
    /// <summary>Name of field for sort</summary>
    [Required]
    public string Column { get; set; } = "Id";

    [Required]
    public DirectionType Direction { get; set; } = DirectionType.Desc;
}

public class Sorting<TRecord> : Sorting
    where TRecord : class
{
}