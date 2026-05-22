using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

public record WorkflowValidationError(string Code, string Message, string? Target = null);

public interface IWorkflowValidator
{
    /// <summary>
    /// Checks invariants on a workflow (cycle-free DAG, references exist, ports known).
    /// Returns empty list if valid.
    /// </summary>
    IReadOnlyList<WorkflowValidationError> Validate(JsonElement workflow);
}
