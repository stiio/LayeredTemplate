using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// Universal start signal for the workflow engine. Replaces submit-specific entry points —
/// any source (form submission, contact created, scheduled tick, …) builds an intent,
/// pairs it with a <see cref="WorkflowDefinition"/>, and hands both to <c>IWorkflowRunner</c>.
/// The trigger source decides what goes into <see cref="Variables"/>; the engine is agnostic.
/// </summary>
public record WorkflowStartIntent
{
    /// <summary>
    /// Stable id of the trigger source — see <see cref="WorkflowTriggerKinds"/>. Stored on the
    /// run for observability; not used by the engine to gate execution.
    /// </summary>
    public required string TriggerKind { get; init; }

    /// <summary>Multi-tenant key. Engine treats it as opaque; in app land this maps to a workspace.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// Polymorphic origin: <c>"Submission"</c>, <c>"Contact"</c>, <c>"Manual"</c>, …. Captured
    /// for trace and so handlers like <c>GetSubmissionWorkflowRunHandler</c> can find the run
    /// without knowing the trigger's internals.
    /// </summary>
    public string? TriggerSourceKind { get; init; }

    /// <summary>Id within <see cref="TriggerSourceKind"/> (e.g. submission id). Null for sourceless triggers (Manual, dry-run).</summary>
    public Guid? TriggerSourceId { get; init; }

    /// <summary>
    /// Trigger-supplied payload as a JSON object. Stored verbatim under the <c>vars</c> namespace
    /// in the run's static context, addressable from templates as <c>{{ vars.&lt;key&gt; }}</c> /
    /// <c>vars.&lt;key&gt;</c>. The trigger source is responsible for shape; e.g.
    /// <c>SubmissionCompleted</c> puts <c>{ answers, meta, submission, form, workspace }</c> here.
    /// <para>
    /// Why JSON and not a dict: the engine's expression resolution path is JSON-typed end to end
    /// (it serializes to <c>static_context</c>, persists, then deserializes through
    /// <c>JsonElementToClr</c> at evaluation time). Accepting a CLR dict at the API boundary
    /// invited consumers to pass arbitrary POCOs / <c>JsonElement</c> mixed shapes that would
    /// either silently round-trip through JSON or — worse — be inconsistent. <see cref="JsonElement"/>
    /// makes the contract explicit: build it with <c>JsonSerializer.SerializeToElement(new { … })</c>
    /// and the serializer's camelCase / null-handling rules are the only knobs that matter.
    /// </para>
    /// <para>
    /// Engine metadata lives under a separate <c>trigger</c> namespace
    /// (<c>{{ trigger.kind }}</c>, <c>{{ trigger.isDryRun }}</c>, …) — the two namespaces never
    /// overlap, so no key in <see cref="Variables"/> can collide with engine-supplied data.
    /// </para>
    /// <para>
    /// <c>null</c> is treated as an empty object. A non-null value with
    /// <see cref="JsonElement.ValueKind"/> other than <see cref="JsonValueKind.Object"/> is a
    /// programming error — the runner throws on dispatch.
    /// </para>
    /// </summary>
    public JsonElement? Variables { get; init; }

    /// <summary>Authenticated user who caused the trigger, if any. Null for anonymous public flows.</summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>
    /// Optional human-friendly run label seeded at dispatch time. Lets the trigger pre-fill
    /// <see cref="WorkflowRunRecord.Name"/> without needing a graph-side <c>SetRunName</c>
    /// action. The action — when present — overrides this value. Plaintext, max 256 chars,
    /// trimmed; <b>not</b> a place for PHI.
    /// </summary>
    public string? Name { get; init; }

    public bool IsDryRun { get; init; }

    /// <summary>
    /// Depth in the parent → child run chain. Top-level triggers (form submit, manual API)
    /// pass 0; the engine increments this when a <c>RunWorkflow</c> action spawns a child.
    /// Capped by <c>WorkflowEngineSettings.MaxNestingLevel</c>.
    /// </summary>
    public int NestingLevel { get; init; }

    /// <summary>
    /// Set by the <c>RunWorkflow</c> action to wire the new run back to its parent. Null for
    /// top-level triggers.
    /// </summary>
    public Guid? ParentRunId { get; init; }

    /// <summary>
    /// Set by the <c>RunWorkflow</c> action when started in <c>waitForCompletion</c> mode — the
    /// suspended parent step that should be resumed once this run reaches a terminal state.
    /// Null in fire-and-forget mode and for top-level triggers.
    /// </summary>
    public Guid? ParentStepId { get; init; }
}
