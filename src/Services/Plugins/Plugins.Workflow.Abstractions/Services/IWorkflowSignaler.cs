using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// External entry point for resuming every run parked on a given opaque correlation key —
/// the generic counterpart of <see cref="IWorkflowDispatcher"/> for the suspend/resume side.
/// An action suspends with one or more <see cref="Actions.WorkflowBookmarkRegistration"/>; the
/// engine persists a bookmark per registration; this facade looks up all bookmarks for the
/// (tenant, key) pair and resumes each waiting step via <see cref="IWorkflowResumer"/> — a
/// fan-out: 2+ runs waiting on the same key are all resumed by one signal.
/// <para>
/// Domain-agnostic by design: the key is a plain string the caller owns. Tenant-scoped on every
/// lookup AND resume — a key in tenant A is invisible to a signal in tenant B. The plugin owns
/// its own DbContext, so calling this from any app unit-of-work is safe (it flushes per resume).
/// </para>
/// </summary>
public interface IWorkflowSignaler
{
    /// <summary>
    /// Resume every run with a bookmark matching <paramref name="correlationKey"/> within
    /// <paramref name="tenantId"/>, injecting <paramref name="payload"/> into each resumed step's
    /// outputs (addressable downstream as <c>steps.&lt;node_key&gt;.*</c>). Idempotent: a second
    /// signal after delivery (bookmarks consumed) returns <c>Delivered = 0</c>; a bookmark whose
    /// step is no longer Waiting (resumed elsewhere / timed out) is counted <c>Stale</c> and
    /// deleted without a double-resume.
    /// </summary>
    Task<WorkflowSignalResult> SignalAsync(
        Guid tenantId,
        string correlationKey,
        JsonElement? payload,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of a <see cref="IWorkflowSignaler.SignalAsync"/> fan-out.
/// </summary>
/// <param name="Delivered">Bookmarks whose step was successfully resumed by this signal.</param>
/// <param name="Stale">
/// Bookmarks that pointed at a no-longer-Waiting step (a race the resumer rejected as
/// StepNotWaiting / ConcurrencyConflict). They are deleted, not resumed — an operational signal
/// that the reconciliation sweep / a prior resume already handled the step.
/// </param>
public record WorkflowSignalResult(int Delivered, int Stale);
