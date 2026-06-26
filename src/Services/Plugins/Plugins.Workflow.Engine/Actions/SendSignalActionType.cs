using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Generic engine-level signal emitter: resolves an opaque correlation key + optional payload and
/// fans the signal out to every run waiting on that key (via <see cref="IWorkflowSignaler"/>). The
/// counterpart of <see cref="WaitSignalActionType"/> — together they let any workflow coordinate
/// with any other (or with a later run of itself) over author-chosen opaque keys, with no
/// form/contact/PHI knowledge in the engine.
/// <para>
/// <b>Re-entrancy / ordering (important):</b> <c>SignalAsync</c> RESUMES other waiting steps, which
/// mutates + flushes the engine store. Calling it from inside an action's <see cref="ExecuteAsync"/>
/// — mid-batch, on the worker thread — is new (the App calls SignalAsync post-commit, not the engine
/// mid-execute). Two facts make it safe:
/// <list type="number">
///   <item>The signal runs in its OWN DI scope (fresh <c>IWorkflowSignaler</c> + DbContext), so its
///   <c>SaveChangesAsync</c> commits independently of — and strictly without interleaving with — the
///   worker's not-yet-flushed mutations for THIS step. The worker applies + flushes the SendSignal
///   result on its batch context AFTER <see cref="ExecuteAsync"/> returns; the signal's unit of work
///   is already committed on the side scope. No synchronous recursion into the engine's flush of THIS
///   step.</item>
///   <item>The resumer's guarded resume (<c>WHERE status='waiting'</c>) makes each waiter resume
///   exactly once and enqueues its successor steps as <b>Pending</b> — they're picked up on a later
///   poll, never executed inline. So a SendSignal that resumes a step which itself sends a signal
///   cannot infinite-loop within one batch: the cascade is bounded by re-claims across poll cycles,
///   each step resuming at most once.</item>
/// </list>
/// </para>
/// </summary>
public class SendSignalActionType : ActionType<SendSignalConfig>
{
    public const string KindName = "SendSignal";

    private const string PortSent = "sent";
    private const string PortError = "error";

    public static readonly IReadOnlyList<ActionPortDescriptor> Ports = new[]
    {
        new ActionPortDescriptor(PortSent, "Sent", ActionPortKind.Normal),
        new ActionPortDescriptor(PortError, "Error", ActionPortKind.Error),
    };

    // Lazy resolution via IServiceScopeFactory — the signaler resumes OTHER waiting steps and flushes
    // the store as its own unit of work; running it on a SEPARATE scope's DbContext keeps that commit
    // from interleaving with the worker's not-yet-flushed mutations for THIS step (see class remarks).
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<SendSignalActionType> logger;

    public SendSignalActionType(
        IServiceScopeFactory scopeFactory,
        ILogger<SendSignalActionType> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    public override string Kind => KindName;

    public override string DisplayName => "Send signal";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Ports;

    public override async Task<ActionExecutionResult> ExecuteAsync(
        ActionContext<SendSignalConfig> context, CancellationToken cancellationToken)
    {
        var key = context.Config.Key?.Resolved?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            // Can't signal "nothing" — fail loud (non-transient) rather than no-op silently.
            return ActionExecutionResult.OnError(
                "SendSignal requires a non-empty correlation key; the key expression resolved empty.",
                transient: false);
        }

        // A waiting bookmark could never have been registered for a key longer than the
        // workflow_bookmark.correlation_key varchar(256), so a signal on an over-long key can match
        // nothing — and the symmetric WaitSignal guard rejects such keys at suspend. Fail loud
        // (non-transient) for parity rather than fan out a guaranteed-zero-delivery lookup.
        if (key.Length > WorkflowBookmarkRegistration.MaxCorrelationKeyLength)
        {
            return ActionExecutionResult.OnError(
                $"SendSignal correlation key must not exceed {WorkflowBookmarkRegistration.MaxCorrelationKeyLength} characters.",
                transient: false);
        }

        var payload = ParsePayload(context.Config.Payload?.Resolved);

        try
        {
            // Fresh scope → fresh IWorkflowSignaler + DbContext. The signaler's per-resume flush
            // commits on THIS scope, decoupled from the worker's batch context (re-entrancy safety).
            await using var scope = this.scopeFactory.CreateAsyncScope();
            var signaler = scope.ServiceProvider.GetRequiredService<IWorkflowSignaler>();
            var result = await signaler.SignalAsync(context.TenantId, key, payload, cancellationToken);

            return this.Port(PortSent, new
            {
                delivered = result.Delivered,
                stale = result.Stale,
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort-but-surfaced: a genuine signaler failure (DB blip mid-fan-out) routes to the
            // `error` port so authors can wire a fallback, rather than silently dropping the signal.
            // The full exception (incl. message) is logged server-side ONLY — the port output carries
            // just a stable reason, never the raw message: an Npgsql/PostgresException can leak query /
            // table / schema fragments into the run record + UI. The exception TYPE name is a safe,
            // coarse hint for triage without exposing internals.
            this.logger.LogError(ex, "SendSignal fan-out failed for tenant {TenantId}.", context.TenantId);
            return this.Port(PortError, new
            {
                reason = "signal_failed",
                errorType = ex.GetType().Name,
            });
        }
    }

    /// <summary>
    /// Parse-as-JSON-else-wrap, mirroring the resume-payload / NormalizeOutputs convention: an author
    /// payload that is valid JSON (object / array / scalar) flows through verbatim so a downstream
    /// <c>WaitSignal</c> can read <c>steps.&lt;key&gt;.fieldName</c>; a non-JSON string is preserved
    /// as a JSON string value rather than dropped. Null / blank → no payload.
    /// </summary>
    private static JsonElement? ParsePayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not JSON — wrap the raw scalar string so it survives as a typed payload.
            return JsonSerializer.SerializeToElement(raw, WorkflowJsonOptions.Default);
        }
    }
}

public class SendSignalConfig
{
    /// <summary>Expression resolving to the opaque correlation key to signal. Required (blank → error).</summary>
    public Expr<string>? Key { get; set; }

    /// <summary>
    /// Optional payload expression. Resolved string is parsed as JSON when possible (so a waiting
    /// step reads <c>steps.&lt;key&gt;.*</c>), otherwise wrapped as a JSON string. Null = no payload.
    /// </summary>
    public Expr<string>? Payload { get; set; }
}
