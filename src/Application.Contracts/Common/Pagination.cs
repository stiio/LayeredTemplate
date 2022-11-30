using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Application.Contracts.Common;

/// <summary>
/// Pagination
/// </summary>
public class Pagination
{
    /// <summary>
    /// Page number (default: 1)
    /// </summary>
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int? Page { get; set; }

    /// <summary>
    /// Limit items on page (default: 10)
    /// </summary>
    /// <example>10</example>
    [Range(1, 100)]
    public int? Limit { get; set; }

    /// <summary>
    /// Total items
    /// </summary>
    /// <example>100</example>
    public int? Total { get; set; }
}