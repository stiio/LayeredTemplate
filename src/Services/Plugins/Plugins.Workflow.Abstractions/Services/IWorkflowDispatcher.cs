using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Single entry point to start a workflow run. Encapsulates the three steps callers used to
/// have to wire up themselves (find the matching <c>WorkflowDefinition</c>, ask
/// <c>IWorkflowRunner.Start</c> to build the run, flush via <c>IWorkflowStore.SaveChangesAsync</c>)
/// behind one async method that's safe to call from any unit-of-work — the plugin owns its
/// own DbContext now, so its commit doesn't piggyback on the consumer's <c>SaveChanges</c>.
/// <para>
/// Intended use is fire-and-forget from app handlers (submit, update, dry-run, …): build a
/// <see cref="WorkflowDispatchRequest"/>, hand it over, get back a structured result. The
/// dispatcher returns <see cref="WorkflowDispatchResult.NotConfigured"/> instead of throwing
/// when no definition exists for the (tenant, owner, trigger) tuple — that's a legitimate
/// state, not an error.
/// </para>
/// </summary>
public interface IWorkflowDispatcher
{
    /// <summary>
    /// Starts a run for the matching definition. <paramref name="flush"/> controls the final
    /// <c>store.SaveChangesAsync</c>: external callers (app handlers, integration tests) keep
    /// the default <c>true</c> so dispatch is a self-contained unit of work; engine-internal
    /// callers (the <c>RunWorkflow</c> action) pass <c>false</c> so the staged child run commits
    /// atomically with the dispatching step's own transition in the worker's per-step flush —
    /// the child can never become claimable before the parent step's state (<c>Waiting</c> in
    /// wait-for-completion mode) is durable, and a crash before that flush re-dispatches exactly
    /// one child instead of leaking an orphan.
    /// </summary>
    Task<WorkflowDispatchResult> DispatchAsync(
        WorkflowDispatchRequest request,
        CancellationToken cancellationToken,
        bool flush = true);
}

/// <summary>
/// Inputs to a dispatch call. Combines the <c>(tenant, owner, trigger)</c> definition lookup
/// keys with the <see cref="Models.WorkflowStartIntent"/> fields the runner needs.
/// </summary>
public record WorkflowDispatchRequest
{
    /// <summary>
    /// Multi-tenant key the RUN is created under — the run's <c>TenantId</c>, all of its steps, and
    /// every action-level tenant/permission check key off this. For an ordinary run it is also the
    /// tenant the definition is looked up in (<see cref="OwnerTenantId"/> defaults to it).
    /// </summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// Tenant the DEFINITION is resolved under, when it differs from the tenant the run executes in
    /// (ADR-028). Null — the default — means "same tenant as the run": the dispatcher looks the
    /// definition up under <see cref="TenantId"/>, exactly as before (zero behavioural change for
    /// every existing caller). Set it only for platform-authored system workflows: the definition
    /// lives under a sentinel tenant, but the run must still be created under the operator's real
    /// workspace (<see cref="TenantId"/>) so the executed actions see the right tenant's data.
    /// </summary>
    public Guid? OwnerTenantId { get; init; }

    /// <summary>Owner kind for the definition lookup (e.g. <c>"Form"</c>).</summary>
    public required string OwnerKind { get; init; }

    /// <summary>Owner id within <see cref="OwnerKind"/>. Null for tenant-scoped definitions.</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Trigger kind constant — drives definition lookup and is recorded on the run.</summary>
    public required string TriggerKind { get; init; }

    /// <summary>Polymorphic trigger source kind — e.g. <c>"Submission"</c>. Null for sourceless triggers (manual / dry-run).</summary>
    public string? TriggerSourceKind { get; init; }

    /// <summary>Id within <see cref="TriggerSourceKind"/>. Null when source-less.</summary>
    public Guid? TriggerSourceId { get; init; }

    /// <summary>Authenticated user who caused the dispatch, if any.</summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>Builder-initiated test runs set this true so the trace UI can mark them and operator dashboards filter them out.</summary>
    public bool IsDryRun { get; init; }

    /// <summary>
    /// Trigger-supplied payload as a JSON object. Stored under the <c>vars</c> namespace in the
    /// run's static context (templates address keys as <c>{{ vars.&lt;key&gt; }}</c>). Trigger
    /// source decides shape; e.g. <c>SubmissionCompleted</c> uses
    /// <c>{ answers, meta, submission, form, workspace }</c>.
    /// <para>
    /// See <see cref="Models.WorkflowStartIntent.Variables"/> for why this is
    /// <see cref="JsonElement"/> rather than a CLR dictionary, and the null / non-object
    /// handling rules.
    /// </para>
    /// </summary>
    public JsonElement? Variables { get; init; }

    /// <summary>
    /// Depth of the requesting run in the parent → child chain. Top-level callers (form submit,
    /// manual API) leave this at 0; the <c>RunWorkflow</c> action passes <c>parent.NestingLevel + 1</c>
    /// so the dispatcher can refuse runs that would exceed
    /// <see cref="WorkflowEngineSettings.MaxNestingLevel"/>.
    /// </summary>
    public int NestingLevel { get; init; }

    /// <summary>Parent run id for sub-workflow dispatches; null otherwise.</summary>
    public Guid? ParentRunId { get; init; }

    /// <summary>
    /// Suspended parent step to resume on terminal state (only set in <c>waitForCompletion</c>
    /// mode). Null for fire-and-forget children and top-level runs.
    /// </summary>
    public Guid? ParentStepId { get; init; }
}

public enum WorkflowDispatchOutcome
{
    /// <summary>Run was created and persisted. <see cref="WorkflowDispatchResult.RunId"/> is set.</summary>
    Started,

    /// <summary>No definition matches the (tenant, owner, trigger) tuple — legitimate, not an error.</summary>
    NotConfigured,

    /// <summary>Definition exists but its graph is empty / has no start nodes — nothing to run.</summary>
    EmptyGraph,

    /// <summary>
    /// Request would create a run at depth &gt; <c>MaxNestingLevel</c>. Dispatcher refuses to start
    /// the run; the calling <c>RunWorkflow</c> action surfaces this as a non-transient error.
    /// </summary>
    NestingLimitExceeded,

    /// <summary>
    /// Parent run already spawned <c>MaxSubRunsPerRun</c> direct children. Dispatcher refuses to
    /// create another; the calling <c>RunWorkflow</c> action surfaces this on its <c>error</c>
    /// port with reason <c>sub_run_limit_exceeded</c>. Only counted for direct children — each
    /// run has its own quota.
    /// </summary>
    SubRunLimitExceeded,
}

public class WorkflowDispatchResult
{
    public WorkflowDispatchOutcome Outcome { get; init; }

    public Guid? RunId { get; init; }

    /// <summary>
    /// Status of the started run at dispatch time (set only when <see cref="Outcome"/> is
    /// <see cref="WorkflowDispatchOutcome.Started"/>; null otherwise). Normally <c>running</c>, but a
    /// run whose start step is dead-on-arrival (its config failed to resolve — e.g. invalid Liquid)
    /// is already <c>failed</c> here. Callers that would otherwise wait on the run (RunWorkflow in
    /// <c>waitForCompletion</c> mode) use this to fire a terminal port immediately instead of
    /// suspending for a resume that will never arrive.
    /// </summary>
    public string? RunStatus { get; init; }

    public bool Started => this.Outcome == WorkflowDispatchOutcome.Started;

    public static WorkflowDispatchResult NotConfigured() =>
        new() { Outcome = WorkflowDispatchOutcome.NotConfigured };

    public static WorkflowDispatchResult EmptyGraph() =>
        new() { Outcome = WorkflowDispatchOutcome.EmptyGraph };

    public static WorkflowDispatchResult NestingLimitExceeded() =>
        new() { Outcome = WorkflowDispatchOutcome.NestingLimitExceeded };

    public static WorkflowDispatchResult SubRunLimitExceeded() =>
        new() { Outcome = WorkflowDispatchOutcome.SubRunLimitExceeded };

    public static WorkflowDispatchResult StartedAt(Guid runId, string runStatus) =>
        new() { Outcome = WorkflowDispatchOutcome.Started, RunId = runId, RunStatus = runStatus };
}
