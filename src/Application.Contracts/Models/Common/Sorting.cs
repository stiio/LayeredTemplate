using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Enums;

namespace LayeredTemplate.Application.Contracts.Models.Common;

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Sorting
/// </summary>
public class Sorting
{
    /// <summary>Name of field for sort</summary>
    [Required]
    public string Column { get; set; } = "Id";

    /// <summary>Direction</summary>
    [Required]
    public DirectionType Direction { get; set; } = DirectionType.Desc;
}

/// <summary>
/// Sorting
/// </summary>
/// <typeparam name="TRecord">Target record type</typeparam>
public class Sorting<TRecord> : Sorting
    where TRecord : class
{
}