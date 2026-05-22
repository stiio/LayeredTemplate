namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// Pagination knob for engine list queries (<see cref="Services.IWorkflowStore.ListRunsAsync"/>,
/// <see cref="Services.IWorkflowStore.ListDefinitionsAsync"/>). Engine-owned record so the store
/// contract doesn't depend on the consumer's framework-specific DTOs (e.g. App's annotated
/// <c>PaginationRequest</c> with FluentValidation rules sits in the controller layer; this one
/// is plain and validates by throwing — both layers stay decoupled).
/// </summary>
/// <param name="Page">1-based page number. Must be ≥ 1.</param>
/// <param name="Limit">Page size. Must be in [1, <see cref="MaxLimit"/>] inclusive.</param>
public record WorkflowPagination(int Page = 1, int Limit = 20)
{
    /// <summary>
    /// Hard cap on items per page. Enforced by <see cref="Validate"/>; protects against
    /// pathological queries that would scan/transfer the whole table in one request.
    /// </summary>
    public const int MaxLimit = 100;

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> on invalid Page / Limit. Stores call
    /// this at the top of every list method so callers get a clean fail-fast on bad inputs.
    /// </summary>
    public void Validate()
    {
        if (this.Page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(this.Page), this.Page, "Page must be ≥ 1.");
        }
        if (this.Limit < 1 || this.Limit > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(this.Limit), this.Limit, $"Limit must be in [1, {MaxLimit}].");
        }
    }

    /// <summary>Number of rows to skip — derived from Page/Limit.</summary>
    public int Offset => (this.Page - 1) * this.Limit;
}
