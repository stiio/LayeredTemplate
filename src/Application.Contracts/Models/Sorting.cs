using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Enums;

namespace LayeredTemplate.Application.Contracts.Models;

/// <summary>
/// Sorting
/// </summary>
public class Sorting
{
    /// <summary>Name of field for sort</summary>
    [Required]
    public string Column { get; set; } = null!;

    /// <summary>Direction</summary>
    [Required]
    public DirectionType Direction { get; set; }
}