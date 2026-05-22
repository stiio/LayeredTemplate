namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// Generic paged result returned by engine list queries. Carries both the page slice and the
/// total count so consumers can render full paginators ("page 3 of 12, 245 items"). Total is a
/// separate query — stores typically issue COUNT alongside the slice; on tables with millions
/// of rows that's a real cost, but the alternative (token-based pagination) is harder to wire
/// into an admin UI that wants jump-to-page.
/// </summary>
public record WorkflowPagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>1-based page number actually returned (echoes <c>PaginationRequest.Page</c>).</summary>
    public required int Page { get; init; }

    /// <summary>Page size actually used (echoes <c>PaginationRequest.Limit</c>).</summary>
    public required int Limit { get; init; }

    /// <summary>Total matching rows across all pages, ignoring page slice.</summary>
    public required long TotalCount { get; init; }

    /// <summary>Convenience: total pages, ceiling of TotalCount / Limit. Always ≥ 1.</summary>
    public int TotalPages => this.Limit < 1 ? 1 : Math.Max(1, (int)Math.Ceiling((double)this.TotalCount / this.Limit));
}
