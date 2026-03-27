using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.App.Application.Common.Models;

#pragma warning disable SA1402 // File may only contain a single type

public class Sorting
{
    /// <summary>Name of field for sort</summary>
    [Required]
    public string Column { get; set; } = "Id";

    [Required]
    public DirectionType Direction { get; set; } = DirectionType.Desc;
}

// ReSharper disable once UnusedTypeParameter
public class Sorting<TRecord> : Sorting
    where TRecord : class
{
}