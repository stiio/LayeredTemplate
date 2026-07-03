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
    private readonly WorkflowRunRecord run;

    public FakeStore(WorkflowRunRecord run)
    {
        this.run = run;
    }

    public List<WorkflowStepRecord> AddedSteps { get; } = new();

    public List<WorkflowStepRecord> UpdatedSteps { get; } = new();

    public List<(WorkflowStepRecord Step, IReadOnlyList<WorkflowBookmarkRegistration> Registrations)> AddedBookmarks { get; } = new();

    /// <summary>Every transaction handed out by <see cref="BeginTransactionAsync"/>, in order — assert Committed/Disposed.</summary>
    public List<FakeStoreTransaction> Transactions { get; } = new();

    /// <summary>
    /// Seed for <see cref="ClaimExpiredWaitingStepsAsync"/>. Drained in claim order; like the
    /// real store's guarded UPDATE, claiming flips the step to <c>Running</c>.
    /// </summary>
    public Queue<WorkflowStepRecord> ExpiredWaitingSteps { get; } = new();

    /// <summary>What <see cref="GetStepStateSummaryAsync"/> reports. Default: nothing active / waiting / dead.</summary>
    public WorkflowRunStepStateSummary StepStateSummary { get; set; } = new(false, false, false);

    public int SaveCount { get; private set; }

    // ===== Runs =====

    public Task<WorkflowRunRecord?> GetRunAsync(Guid runId, CancellationToken cancellationToken)
        => Task.FromResult<WorkflowRunRecord?>(runId == this.run.Id ? this.run : null);

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

    public Task<IReadOnlyList<WorkflowStepRecord>> ClaimExpiredWaitingStepsAsync(int limit, CancellationToken cancellationToken)
    {
        var claimed = new List<WorkflowStepRecord>();
        while (claimed.Count < limit && this.ExpiredWaitingSteps.Count > 0)
        {
            var step = this.ExpiredWaitingSteps.Dequeue();
            // Claim flips Waiting → Running (FOR UPDATE SKIP LOCKED in prod); mirror it so the
            // swept step is logically ours before ApplyResult moves it terminal.
            step.Status = StepExecutionStatus.Running;
            claimed.Add(step);
        }

        return Task.FromResult<IReadOnlyList<WorkflowStepRecord>>(claimed);
    }

    public Task<int> ReleaseClaimedStepsAsync(IReadOnlyList<Guid> stepIds, CancellationToken cancellationToken)
        => Task.FromResult(0);

    // ===== Bookmarks =====

    public void AddBookmarks(WorkflowStepRecord step, IReadOnlyList<WorkflowBookmarkRegistration> registrations)
        => this.AddedBookmarks.Add((step, registrations));

    public Task<IReadOnlyList<WorkflowBookmarkRecord>> FindBookmarksAsync(Guid tenantId, string correlationKey, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<WorkflowBookmarkRecord>>(Array.Empty<WorkflowBookmarkRecord>());

    public Task<int> DeleteBookmarksAsync(IReadOnlyList<Guid> bookmarkIds, CancellationToken cancellationToken)
        => Task.FromResult(0);

    public Task<int> SweepResolvedBookmarksAsync(int limit, CancellationToken cancellationToken)
        => Task.FromResult(0);

    // ===== Atomic commit =====

    public Task<IWorkflowStoreTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = new FakeStoreTransaction();
        this.Transactions.Add(transaction);
        return Task.FromResult<IWorkflowStoreTransaction?>(transaction);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        this.SaveCount++;
        return Task.CompletedTask;
    }

    public void DiscardPendingChanges()
    {
    }

    // ===== Unused in these tests =====

    public Task<WorkflowDefinition?> FindDefinitionAsync(Guid tenantId, string ownerKind, Guid? ownerId, string triggerKind, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<WorkflowDefinition?> GetDefinitionByIdAsync(Guid definitionId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

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
        => Task.FromResult(0);

    public Task<bool> AnyRunsForDefinitionAsync(Guid definitionId, CancellationToken cancellationToken)
        => Task.FromResult(false);

    public Task<IReadOnlyList<WorkflowStepRecord>> GetStepsForRunAsync(Guid runId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<WorkflowStepRecord?> GetStepAsync(Guid stepId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<WorkflowStepRecord>> ClaimPendingStepsAsync(int batchSize, WorkflowStepLane lane, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<WorkflowStepRecord?> TryResumeWaitingStepAsync(Guid stepId, string outputPort, object? outputs, CancellationToken cancellationToken)
        => throw new NotSupportedException();

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
