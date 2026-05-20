using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.App.Shared.Pagination;

public sealed class PaginationResponse
{
    /// <summary>Page number (1-based).</summary>
    [Range(1, int.MaxValue)]
    [Required]
    public int Page { get; set; } = 1;

    /// <summary>Items per page.</summary>
    [Range(1, 100)]
    [Required]
    public int Limit { get; set; } = 10;

    /// <summary>Total count across all pages.</summary>
    [Required]
    public long Total { get; set; }
}