using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Application.Contracts.Models.Common;

/// <summary>
/// Pagination Request
/// </summary>
public class PaginationRequest
{
    /// <summary>
    /// Page number (default: 1)
    /// </summary>
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    [Required]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Limit items on page (default: 10)
    /// </summary>
    /// <example>10</example>
    [Range(1, 100)]
    [Required]
    public int Limit { get; set; } = 10;
}