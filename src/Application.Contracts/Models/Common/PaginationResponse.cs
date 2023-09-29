using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Application.Contracts.Models.Common;

public class PaginationResponse
{
    /// <summary>
    /// Page number
    /// </summary>
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    [Required]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Limit items on page
    /// </summary>
    /// <example>10</example>
    [Range(1, 100)]
    [Required]
    public int Limit { get; set; } = 10;

    /// <summary>
    /// Total items
    /// </summary>
    /// <example>100</example>
    [Required]
    public int Total { get; set; }
}