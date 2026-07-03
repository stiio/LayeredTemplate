using LayeredTemplate.Plugins.Workflow.Abstractions.Services;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// Storage-plugin knobs, set at composition time via
/// <c>AddEfCoreStorage(connectionString, configure: o =&gt; …)</c>. Deliberately not
/// IOptions-bound: these are wiring choices, not environment configuration.
/// </summary>
public sealed class WorkflowEfCoreStorageOptions
{
    /// <summary>
    /// When true (default) the plugin wires Postgres LISTEN/NOTIFY as a work push channel: a
    /// SaveChanges interceptor NOTIFYs whenever a flush makes steps claimable, and one
    /// per-process listener connection pulses <see cref="IWorkflowWorkSignal"/> so idle worker
    /// loops wake within milliseconds instead of waiting out the fallback
    /// <c>PollIntervalSeconds</c>. Purely an accelerator: the claim query over the database
    /// stays the source of truth, so a disabled or broken push channel (e.g. PgBouncer in
    /// transaction-pooling mode, which cannot carry LISTEN) costs latency, never correctness.
    /// </summary>
    public bool EnableListenNotify { get; set; } = true;

    /// <summary>
    /// NOTIFY channel name. Channels are global per Postgres database — override when several
    /// independent app installations share one database. Must be a plain identifier
    /// (letters / digits / underscore, not starting with a digit).
    /// </summary>
    public string ListenNotifyChannel { get; set; } = "workflow_work";
}
