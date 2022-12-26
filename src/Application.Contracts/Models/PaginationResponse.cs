using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Application.Contracts.Models;

/// <summary>
/// Pagination Response
/// </summary>
public class PaginationResponse : PaginationRequest
{
    /// <summary>
    /// Total items
    /// </summary>
    /// <example>100</example>
    [Required]
    public int Total { get; set; }
}