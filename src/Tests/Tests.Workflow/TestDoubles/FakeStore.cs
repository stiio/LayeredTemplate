using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;

namespace LayeredTemplate.Tests.Workflow.TestDoubles;

/// <summary>
/// Minimal in-memory <see cref="IWorkflowStore"/> — only the surface the engine's execute and
/// timeout-sweep paths touch. Methods unused by the current tests throw so accidental coverage
/// gaps surface loud. Ported from the origin project's suite and adapted to the current
/// interface: targeted visit count, lane-less expired claim, storage transactions.
/// </summary>
internal class FakeStore : IWorkflowStore
{
    private readonly WorkflowRunRecord? run;

    public FakeStore(WorkflowRunRecord? run = null)
    {
        this.run = run;
    }

    public List<WorkflowStepRecord> AddedSteps { get; } = new();

    public List<WorkflowStepRecord> UpdatedSteps { get; } = new();

    public List<(WorkflowStepRecord Step, IReadOnlyList<WorkflowBookmarkRegistration> Registrations)> AddedBookmarks { get; } = new();

    /// <summary>Every transaction handed out by <see cref="BeginTransactionAsync"/>, in order — assert Committed/Disposed.</summary>
    public List<FakeStoreTransaction> Transactions { get; } = new();

    /// <summary>
    /// Seed for <see cref="ClaimExpiredWaitingStepIdsAsync"/>. Drained in claim order; like the
    /// real store's guarded UPDATE, claiming flips the step to <c>Running</c>.
    /// </summary>
    public Queue<WorkflowStepRecord> ExpiredWaitingSteps { get; } = new();

    /// <summary>What <see cref="GetStepStateSummaryAsync"/> reports. Default: nothing active / waiting / dead.</summary>
    public WorkflowRunStepStateSummary StepStateSummary { get; set; } = new(false, false, false);

    /// <summary>Seed for <see cref="GetStepAsync"/> / <see cref="TryResumeWaitingStepAsync"/> lookups (resume-path tests).</summary>
    public List<WorkflowStepRecord> Steps { get; } = new();

    /// <summary>Seed for <see cref="FindBookmarksAsync"/> (signal-path tests). Lookup is tenant-scoped like prod.</summary>
    public List<WorkflowBookmarkRecord> Bookmarks { get; } = new();

    /// <summary>Every id handed to <see cref="DeleteBookmarksAsync"/> — assert eager cleanup.</summary>
    public List<Guid> DeletedBookmarkIds { get; } = new();

    /// <summary>
    /// When true, <see cref="BeginTransactionAsync"/> returns null — simulates an ambient
    /// transaction already open on the scope (the resumer's chain-unwind participation mode).
    /// </summary>
    public bool SimulateAmbientTransaction { get; set; }

    /// <summary>What <see cref="FindDefinitionAsync"/> returns (dispatcher-path tests). Default: none.</summary>
    public WorkflowDefinition? Definition { get; set; }

    public bool FindDefinitionCalled { get; private set; }

    /// <summary>What <see cref="GetDefinitionByIdAsync"/> returns (restarter's live-definition mode). Default: none.</summary>
    public WorkflowDefinition? LiveDefinition { get; set; }

    public bool GetDefinitionByIdCalled { get; private set; }

    /// <summary>What <see cref="CountChildRunsAsync"/> reports (sub-run cap tests). Default 0.</summary>
    public int ChildRunCount { get; set; }

    public bool CountChildRunsCalled { get; private set; }

    public int SaveCount { get; private set; }

    // ===== Runs =====

    public Task<WorkflowRunRecord?> GetRunAsync(Guid runId, CancellationToken cancellationToken)
        => Task.FromResult(runId == this.run?.Id ? this.run : null);

    public void UpdateRun(WorkflowRunRecord r)
    {
    }

    public void AddRun(WorkflowRunRecord r) => throw new NotSupportedException();

    // ===== Steps =====

    public void AddStep(WorkflowStepRecord step) => this.AddedSteps.Add(step);

    public void UpdateStep(WorkflowStepRecord step) => this.UpdatedSteps.Add(step);

    public Task<int> CountStepsForRunAsync(Guid runId, CancellationToken cancellationToken)
        => Task.FromResult(this.AddedSteps.Count);

    public Task<int> CountVisitsForNodeAsync(Guid runId, string nodeId, CancellationToken cancellationToken)
        => Task.FromResult(this.AddedSteps.Count(s => s.RunId == runId && s.NodeId == nodeId));

    public Task<WorkflowRunStepStateSummary> GetStepStateSummaryAsync(
        Guid runId, Guid excludingStepId, CancellationToken cancellationToken)
        => Task.FromResult(this.StepStateSummary);

    public Task<IReadOnlyList<Guid>> ClaimExpiredWaitingStepIdsAsync(int limit, CancellationToken cancellationToken)
    {
        var claimedIds = new List<Guid>();
        while (claimedIds.Count < limit && this.ExpiredWaitingSteps.Count > 0)
        {
            var step = this.ExpiredWaitingSteps.Dequeue();
            // Claim flips Waiting → Running (FOR UPDATE SKIP LOCKED in prod); mirror it so the
            // swept step is logically ours before ApplyResult moves it terminal. Register the
            // step for the follow-up GetStepAsync load, like the real row would be found by id.
            step.Status = StepExecutionStatus.Running;
            if (!this.Steps.Contains(step))
            {
                this.Steps.Add(step);
            }

            claimedIds.Add(step.Id);
        }

        return Task.FromResult<IReadOnlyList<Guid>>(claimedIds);
    }

    public Task<int> ReleaseClaimedStepsAsync(IReadOnlyList<Guid> stepIds, CancellationToken cancellationToken)
        => Task.FromResult(0);

    // ===== Bookmarks =====

    public void AddBookmarks(WorkflowStepRecord step, IReadOnlyList<WorkflowBookmarkRegistration> registrations)
        => this.AddedBookmarks.Add((step, registrations));

    public Task<IReadOnlyList<WorkflowBookmarkRecord>> FindBookmarksAsync(Guid tenantId, string correlationKey, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<WorkflowBookmarkRecord>>(
            this.Bookmarks.Where(b => b.TenantId == tenantId && b.CorrelationKey == correlationKey).ToList());

    public Task<int> DeleteBookmarksAsync(IReadOnlyList<Guid> bookmarkIds, CancellationToken cancellationToken)
    {
        this.DeletedBookmarkIds.AddRange(bookmarkIds);
        this.Bookmarks.RemoveAll(b => bookmarkIds.Contains(b.Id));
        return Task.FromResult(bookmarkIds.Count);
    }

    public Task<int> SweepResolvedBookmarksAsync(int limit, CancellationToken cancellationToken)
        => Task.FromResult(0);

    // ===== Atomic commit =====

    public Task<IWorkflowStoreTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (this.SimulateAmbientTransaction)
        {
            return Task.FromResult<IWorkflowStoreTransaction?>(null);
        }

        var transaction = new FakeStoreTransaction();
        this.Transactions.Add(transaction);
        return Task.FromResult<IWorkflowStoreTransaction?>(transaction);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        this.SaveCount++;
        return Task.CompletedTask;
    }

    // ===== Definitions / dispatch path =====

    public Task<WorkflowDefinition?> FindDefinitionAsync(Guid tenantId, string ownerKind, Guid? ownerId, string triggerKind, CancellationToken cancellationToken)
    {
        this.FindDefinitionCalled = true;
        return Task.FromResult(this.Definition);
    }

    public Task<WorkflowDefinition?> GetDefinitionByIdAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        this.GetDefinitionByIdCalled = true;
        return Task.FromResult(this.LiveDefinition);
    }

    public Task UpsertDefinitionAsync(Guid tenantId, string ownerKind, Guid? ownerId, string triggerKind, WorkflowGraph graph, string? displayName, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<WorkflowPagedResult<WorkflowDefinition>> ListDefinitionsAsync(WorkflowDefinitionFilter filter, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task DeleteDefinitionAsync(Guid tenantId, string ownerKind, Guid? ownerId, string triggerKind, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<WorkflowRunRecord?> FindRunByTriggerSourceAsync(Guid tenantId, string triggerSourceKind, Guid triggerSourceId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<WorkflowRunRecord>> ListRunsByTriggerSourceAsync(Guid tenantId, string triggerSourceKind, Guid triggerSourceId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<WorkflowPagedResult<WorkflowRunRecord>> ListRunsAsync(WorkflowRunFilter filter, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<int> CountChildRunsAsync(Guid parentRunId, CancellationToken cancellationToken)
    {
        this.CountChildRunsCalled = true;
        return Task.FromResult(this.ChildRunCount);
    }

    public Task<bool> AnyRunsForDefinitionAsync(Guid definitionId, CancellationToken cancellationToken)
        => Task.FromResult(false);

    public Task<IReadOnlyList<WorkflowStepRecord>> GetStepsForRunAsync(Guid runId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<WorkflowStepRecord?> GetStepAsync(Guid stepId, CancellationToken cancellationToken)
        => Task.FromResult(this.Steps.FirstOrDefault(s => s.Id == stepId));

    public Task<IReadOnlyList<Guid>> ClaimPendingStepIdsAsync(int batchSize, WorkflowStepLane lane, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<WorkflowStepRecord?> TryResumeWaitingStepAsync(Guid stepId, string outputPort, object? outputs, CancellationToken cancellationToken)
    {
        // Mirror the real store's atomic guard: flip Waiting → Completed + stamp port/outputs
        // only if the step is still Waiting; anything else loses with null (409-style).
        var step = this.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null || step.Status != StepExecutionStatus.Waiting)
        {
            return Task.FromResult<WorkflowStepRecord?>(null);
        }

        step.Status = StepExecutionStatus.Completed;
        step.OutputPort = outputPort;
        step.Outputs = outputs is null
            ? step.Outputs
            : JsonSerializer.SerializeToElement(outputs, WorkflowJsonOptions.Default);
        step.CompletedAt = DateTime.UtcNow;
        return Task.FromResult<WorkflowStepRecord?>(step);
    }

    public Task<int> PurgeFinishedRunsAsync(DateTime olderThan, int limit, Guid? tenantId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<int> PurgeAllForTenantAsync(Guid tenantId, int limit, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<int> PurgeStaleRunningRunsAsync(DateTime olderThan, int limit, Guid? tenantId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<int> PurgeRunsByDefinitionAsync(Guid tenantId, Guid definitionId, int limit, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

/// <summary>Recording <see cref="IWorkflowStoreTransaction"/> — assert commit/rollback semantics.</summary>
internal sealed class FakeStoreTransaction : IWorkflowStoreTransaction
{
    public bool Committed { get; private set; }

    public bool Disposed { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        this.Committed = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        this.Disposed = true;
        return ValueTask.CompletedTask;
    }
}
