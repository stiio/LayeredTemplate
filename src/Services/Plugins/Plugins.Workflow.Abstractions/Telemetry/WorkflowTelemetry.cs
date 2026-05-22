namespace LayeredTemplate.Plugins.Workflow.Abstractions.Telemetry;

/// <summary>
/// Constants the engine emits via <see cref="System.Diagnostics.ActivitySource"/>. Consumers
/// register the source name with their tracing pipeline to receive engine spans:
/// <code>
/// services.AddOpenTelemetry().WithTracing(builder =&gt; builder
///     .AddSource(WorkflowTelemetry.ActivitySourceName)
///     .AddOtlpExporter());          // or Jaeger / Zipkin / Honeycomb / Application Insights / …
/// </code>
/// Without this registration, every engine call to <c>StartActivity</c> returns null and the
/// instrumentation has zero runtime cost.
/// </summary>
/// <remarks>
/// <para>
/// <b>Span shape (high-level):</b>
/// </para>
/// <list type="bullet">
///   <item><c>workflow.run.dispatch</c> — engine receives a new run intent.</item>
///   <item><c>workflow.step.execute</c> — one step is dispatched to its action.</item>
///   <item><c>workflow.action.execute</c> — child of <c>step.execute</c>; wraps the action's
///   <c>ExecuteAsync</c> call. I/O latency lives here.</item>
///   <item><c>workflow.step.timeout</c> — sweeper picked up an expired waiting step.</item>
///   <item><c>workflow.fanout.enqueue_next</c> — edge walking after a step terminates.</item>
///   <item><c>workflow.fanout.check_completion</c> — run-status state machine evaluation.</item>
///   <item><c>workflow.run.resume</c> / <c>workflow.run.cancel</c> / <c>workflow.run.restart</c>
///   — operator-driven state transitions.</item>
///   <item><c>workflow.retention.sweep</c> — retention worker on a non-empty sweep.</item>
/// </list>
/// <para>
/// All spans carry a <c>workflow.run_id</c> tag so traces can be filtered to a single run
/// regardless of where the work happened (background worker, HTTP resume, sub-workflow cascade).
/// </para>
/// </remarks>
public static class WorkflowTelemetry
{
    /// <summary>
    /// ActivitySource name. Matches the engine plugin assembly name — standard OpenTelemetry
    /// convention for component-level sources.
    /// </summary>
    public const string ActivitySourceName = "Hipaa.Backend.Plugins.Workflow.Engine";
}
