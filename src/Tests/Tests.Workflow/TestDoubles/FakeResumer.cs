using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;

namespace LayeredTemplate.Tests.Workflow.TestDoubles;

/// <summary>
/// Recording <see cref="IWorkflowResumer"/>: captures every command and returns a configurable
/// outcome (default: success). Used where the fan-out INTO the resumer is under test (signaler),
/// not the resume itself.
/// </summary>
internal sealed class FakeResumer : IWorkflowResumer
{
    private readonly Func<WorkflowResumeCommand, WorkflowResumeResult> outcome;

    public FakeResumer(Func<WorkflowResumeCommand, WorkflowResumeResult>? outcome = null)
        => this.outcome = outcome ?? (_ => WorkflowResumeResult.Success());

    public List<(Guid RunId, Guid StepId, Guid TenantId, string? Port, JsonElement? Payload)> Commands { get; } = new();

    public Task<WorkflowResumeResult> ResumeAsync(WorkflowResumeCommand command, CancellationToken cancellationToken)
    {
        this.Commands.Add((command.RunId, command.StepId, command.TenantId, command.Port, command.Payload));
        return Task.FromResult(this.outcome(command));
    }
}
