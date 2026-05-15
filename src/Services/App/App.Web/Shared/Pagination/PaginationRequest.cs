using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.App.Shared.Pagination;

public sealed class PaginationRequest
{
    /// <summary>Page number (1-based).</summary>
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    [Required]
    public int Page { get; set; } = 1;

    /// <summary>Items per page.</summary>
    /// <example>10</example>
    [Range(1, 100)]
    [Required]
    public int Limit { get; set; } = 10;
}

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

public enum DirectionType
{
    Asc,
    Desc,
}

// NOTE: XML doc comments on properties of a generic class trigger a duplicate-key crash in
// Microsoft.AspNetCore.OpenApi.SourceGenerators 10.0.x when the type lives in the current
// compilation (not in a referenced assembly). Description of `Column` is therefore omitted
// here and supplied per-usage on the property carrying the Sorting<T>.
public sealed class Sorting<TFields>
    where TFields : Enum
{
    public TFields Column { get; set; } = default!;

    public DirectionType Direction { get; set; } = DirectionType.Desc;
}
