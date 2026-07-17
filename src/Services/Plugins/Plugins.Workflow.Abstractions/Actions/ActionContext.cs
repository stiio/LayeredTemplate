using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

/// <summary>
/// Non-generic ActionContext — used by the engine to dispatch to <c>IActionType.ExecuteAsync</c>.
/// The <see cref="Config"/> is the deserialized + resolved config POCO; the action itself normally
/// sees the typed variant <see cref="ActionContext{TConfig}"/> via the <c>ActionType&lt;TConfig&gt;</c> base.
/// </summary>
public class ActionContext
{
    public object Config { get; init; } = null!;

    public Guid RunId { get; init; }

    public Guid StepExecutionId { get; init; }

    /// <summary>
    /// Tenant the run belongs to (workspace id, in the App's domain language). Custom action types
    /// must scope every entity lookup / mutation to this id and reject anything that resolves to a
    /// different tenant — the engine itself does not enforce isolation beyond passing this through.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>Workflow definition id this run was started from.</summary>
    public Guid DefinitionId { get; init; }

    /// <summary>
    /// User who triggered the run (when known — public form submissions are anonymous, dry-runs
    /// from the builder carry the editor's id, etc). Use for audit trail and "did this principal
    /// have permission to perform X" checks; do <b>not</b> use for tenant scoping.
    /// </summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>What kind of source kicked this run off (e.g. <c>submission_completed</c>).</summary>
    public string? TriggerSourceKind { get; init; }

    /// <summary>Id of the source entity that triggered the run (e.g. submission id).</summary>
    public Guid? TriggerSourceId { get; init; }

    /// <summary>
    /// True when this step belongs to a dry-run triggered from the builder. Actions run normally,
    /// but this flag lets individual action types adjust side effects when needed — e.g. a PDF
    /// generator could emit a short-lived preview file instead of a permanent one.
    /// </summary>
    public bool IsDryRun { get; init; }

    /// <summary>
    /// User-facing slug of the node this step belongs to (the same value <c>steps.&lt;node_key&gt;</c>
    /// uses in templates). Loop / state-aware actions read it to look themselves up inside
    /// <see cref="StepsOutputs"/> — see <c>ForEachActionType</c> for the canonical example.
    /// </summary>
    public string NodeKey { get; init; } = string.Empty;

    /// <summary>
    /// Snapshot of <c>run.steps_outputs</c> at the time this step was claimed: a JSON object mapping
    /// node-key → outputs of the latest completed step on that node. Stable for the duration of
    /// this dispatch. State-aware actions read their own previous outputs via
    /// <c>StepsOutputs.TryGetProperty(NodeKey, out var prev)</c>.
    /// </summary>
    public JsonElement StepsOutputs { get; init; }

    /// <summary>
    /// Outputs THIS step execution persisted on a previous attempt (or at suspend time, for the
    /// resume / timeout hooks) — the retry-checkpoint channel. When a multi-side-effect action
    /// fails partway (row inserted, email not sent), it returns
    /// <c>OnError(..., outputs: checkpoint, transient: true)</c>; the engine persists the
    /// checkpoint on the step row and hands it back here on the next attempt, so the action can
    /// skip work it already did. Null on the first attempt or when no prior attempt returned
    /// outputs. Scoped to this exact step execution — distinct from <see cref="StepsOutputs"/>,
    /// which carries other (completed) steps' outputs.
    /// <para>
    /// Limits: a checkpoint exists only if the failing attempt RETURNED it — a hard crash
    /// mid-action loses that attempt's progress (the previous checkpoint, if any, survives).
    /// For side effects that must never double-fire, idempotency keys remain the robust answer;
    /// this channel makes the common retry cheap, not transactional.
    /// </para>
    /// </summary>
    public JsonElement? PriorAttemptOutputs { get; init; }

    /// <summary>
    /// Which attempt this dispatch is (1-based; the claim increments it). Useful together with
    /// <see cref="PriorAttemptOutputs"/> for retry-aware actions and for logging.
    /// </summary>
    public int AttemptCount { get; init; }
}

/// <summary>Strongly-typed context seen by action implementations.</summary>
public class ActionContext<TConfig> where TConfig : class
{
    public TConfig Config { get; init; } = null!;

    public Guid RunId { get; init; }

    public Guid StepExecutionId { get; init; }

    public Guid TenantId { get; init; }

    public Guid DefinitionId { get; init; }

    public Guid? ActorUserId { get; init; }

    public string? TriggerSourceKind { get; init; }

    public Guid? TriggerSourceId { get; init; }

    public bool IsDryRun { get; init; }

    public string NodeKey { get; init; } = string.Empty;

    public JsonElement StepsOutputs { get; init; }

    /// <inheritdoc cref="ActionContext.PriorAttemptOutputs"/>
    public JsonElement? PriorAttemptOutputs { get; init; }

    /// <inheritdoc cref="ActionContext.AttemptCount"/>
    public int AttemptCount { get; init; }
}
