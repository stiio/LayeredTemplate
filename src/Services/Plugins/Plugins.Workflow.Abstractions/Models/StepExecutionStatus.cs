namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

public static class StepExecutionStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Dead = "dead";

    /// <summary>
    /// Join step accumulating arrivals — not claimable by the worker until <c>Arrivals</c>
    /// reaches <c>ExpectedArrivals</c>, at which point it transitions to <see cref="Pending"/>.
    /// Expired Waiting steps (NextAttemptAt &lt; now) are swept to <see cref="Dead"/>.
    /// </summary>
    public const string Waiting = "waiting";
}
