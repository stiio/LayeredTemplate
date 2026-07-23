using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Services;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Engine-built-in action that writes a Liquid- / JS-computed label onto
/// <c>WorkflowRunRecord.Name</c>. Lets authors derive a human-friendly identifier from the
/// trigger payload (e.g. patient name, intake id, …) so list / detail dashboards can tell two
/// runs apart at a glance.
/// <para>
/// Mid-run mutation goes through <see cref="IWorkflowStore"/> — same scoped store the worker
/// already loaded the run into via <c>GetRunAsync</c>, so the entity is in EF's
/// <c>ChangeTracker.Local</c> and <see cref="IWorkflowStore.UpdateRun"/> picks up the change.
/// The worker's per-step <c>SaveChangesAsync</c> flushes it. No standalone save here — keeping
/// the batch-flush invariant intact.
/// </para>
/// <para>
/// Last write wins: re-entry through ForEach iterations or post-restart replays simply
/// overwrites the previous value. Empty / whitespace result clears the column.
/// </para>
/// <para>
/// PHI policy: the column is <b>plaintext</b> (not routed through
/// <c>WorkflowProtectedStringConverter</c>) so list views can render it without per-row
/// decryption. Authors are explicitly asked not to put PHI into run names; for PHI use
/// protected step outputs instead.
/// </para>
/// </summary>
public class SetRunNameActionType : ActionType<SetRunNameConfig>
{
    public const string KindName = "SetRunName";

    public static readonly IReadOnlyList<ActionPortDescriptor> Ports = new[]
    {
        new ActionPortDescriptor("done", "Done", ActionPortKind.Normal),
    };

    private readonly IWorkflowStore store;

    public SetRunNameActionType(IWorkflowStore store)
    {
        this.store = store;
    }

    public override string Kind => KindName;

    public override string DisplayName => "Set run name";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Ports;

    public override async Task<ActionExecutionResult> ExecuteAsync(
        ActionContext<SetRunNameConfig> context, CancellationToken cancellationToken)
    {
        var name = WorkflowRunner.NormalizeName(context.Config.Name?.Resolved);

        // Scoped store is shared with the step's scope — GetRunAsync hits Local first when
        // the run was already loaded by the step executor, returning the tracked entity.
        var run = await this.store.GetRunAsync(context.RunId, cancellationToken);
        if (run is not null)
        {
            run.Name = name;
            this.store.UpdateRun(run);
        }

        // Surface the resolved label on step outputs so downstream Liquid / JS templates can
        // address it via {{ steps.<key>.name }} without re-rendering the source expression.
        return this.Port("done", new { name });
    }
}

public class SetRunNameConfig
{
    /// <summary>
    /// Liquid / JS expression yielding the label string. Trimmed and capped at 256 chars on
    /// resolve. Empty / whitespace clears the column.
    /// </summary>
    public Expr<string>? Name { get; set; }
}
