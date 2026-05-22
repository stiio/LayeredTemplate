using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Storage.EFCore.Entities;
using Microsoft.EntityFrameworkCore;
using PluginWorkflowDefinition = LayeredTemplate.Plugins.Workflow.Abstractions.Models.WorkflowDefinition;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// EF Core impl of <see cref="IWorkflowStore"/>. Postgres-specific only in the
/// <see cref="ClaimPendingStepsAsync"/> raw SQL (FOR UPDATE SKIP LOCKED + RETURNING).
/// To swap for SQL Server / etc., implement <see cref="IWorkflowStore"/> against the equivalent
/// vendor primitives — the engine is unchanged.
/// </summary>
internal class EfCoreWorkflowStore : IWorkflowStore
{
    private readonly WorkflowDbContext dbContext;

    public EfCoreWorkflowStore(WorkflowDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    // ===== Definitions =====

    public async Task<PluginWorkflowDefinition?> FindDefinitionAsync(
        Guid tenantId, string ownerKind, Guid? ownerId, string triggerKind, CancellationToken cancellationToken)
    {
        var entity = await this.dbContext.WorkflowDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.TenantId == tenantId
                    && d.OwnerKind == ownerKind
                    && d.OwnerId == ownerId
                    && d.TriggerKind == triggerKind,
                cancellationToken);
        return entity is null ? null : MapDefinition(entity);
    }

    public async Task<PluginWorkflowDefinition?> GetDefinitionByIdAsync(
        Guid definitionId, CancellationToken cancellationToken)
    {
        // PK lookup. No tenant filter here — restarter holds the tenant on its old-run handle
        // and re-checks before using the result.
        var entity = await this.dbContext.WorkflowDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        return entity is null ? null : MapDefinition(entity);
    }

    public async Task UpsertDefinitionAsync(
        Guid tenantId, string ownerKind, Guid? ownerId, string triggerKind, WorkflowGraph graph,
        string? displayName, CancellationToken cancellationToken)
    {
        var graphJson = JsonSerializer.Serialize(graph, WorkflowJsonOptions.Default);
        var existing = await this.dbContext.WorkflowDefinitions
            .FirstOrDefaultAsync(
                d => d.TenantId == tenantId
                    && d.OwnerKind == ownerKind
                    && d.OwnerId == ownerId
                    && d.TriggerKind == triggerKind,
                cancellationToken);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            this.dbContext.WorkflowDefinitions.Add(new Entities.WorkflowDefinition
            {
                TenantId = tenantId,
                OwnerKind = ownerKind,
                OwnerId = ownerId,
                TriggerKind = triggerKind,
                DisplayName = displayName,
                Graph = graphJson,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.Graph = graphJson;
            // Null displayName on update means "leave as-is" — lets graph-only saves keep the
            // existing label without consumers needing to re-fetch first to round-trip it.
            if (displayName is not null)
            {
                existing.DisplayName = displayName;
            }
            existing.UpdatedAt = now;
        }
    }

    public async Task<WorkflowPagedResult<PluginWorkflowDefinition>> ListDefinitionsAsync(
        WorkflowDefinitionFilter filter, CancellationToken cancellationToken)
    {
        filter.Pagination.Validate();

        IQueryable<Entities.WorkflowDefinition> query = this.dbContext.WorkflowDefinitions
            .AsNoTracking()
            .Where(d => d.TenantId == filter.TenantId);

        if (filter.OwnerKind is not null)
        {
            query = query.Where(d => d.OwnerKind == filter.OwnerKind);
        }
        if (filter.OwnerId is { } ownerId)
        {
            query = query.Where(d => d.OwnerId == ownerId);
        }
        if (filter.TriggerKind is not null)
        {
            query = query.Where(d => d.TriggerKind == filter.TriggerKind);
        }

        // Total + slice in two queries; could fold into one with window funcs but EF doesn't
        // generate them cleanly and the second roundtrip is cheap relative to the page fetch.
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip(filter.Pagination.Offset)
            .Take(filter.Pagination.Limit)
            .ToListAsync(cancellationToken);

        return new WorkflowPagedResult<PluginWorkflowDefinition>
        {
            Items = items.Select(MapDefinition).ToList(),
            Page = filter.Pagination.Page,
            Limit = filter.Pagination.Limit,
            TotalCount = total,
        };
    }

    public async Task DeleteDefinitionAsync(
        Guid tenantId, string ownerKind, Guid? ownerId, string triggerKind, CancellationToken cancellationToken)
    {
        var existing = await this.dbContext.WorkflowDefinitions
            .FirstOrDefaultAsync(
                d => d.TenantId == tenantId
                    && d.OwnerKind == ownerKind
                    && d.OwnerId == ownerId
                    && d.TriggerKind == triggerKind,
                cancellationToken);
        if (existing is not null) this.dbContext.WorkflowDefinitions.Remove(existing);
    }

    // ===== Runs =====

    public void AddRun(WorkflowRunRecord run)
    {
        var entity = MapRunRecordToEntity(run);
        var now = DateTime.UtcNow;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        this.dbContext.WorkflowRuns.Add(entity);
    }

    public void UpdateRun(WorkflowRunRecord run)
    {
        // Local-only lookup: the worker / fan-out / canceller / restarter all ensure the run
        // is tracked in this scope before mutating (via GetRunAsync), so a Local miss is a
        // logic bug — not a state we want to silently round-trip into. With the shared-scope
        // worker model and no concurrency token, this contract is restored: claim's tracked
        // load + GetRunAsync calls keep the entity alive in Local for the batch's lifetime.
        var entity = this.dbContext.WorkflowRuns.Local.FirstOrDefault(e => e.Id == run.Id);
        if (entity is null) return;
        ApplyRunRecordToEntity(run, entity);
        entity.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<WorkflowRunRecord?> GetRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        // Tracked load — worker mutates fields and we want UpdateRun to see the same instance.
        var entity = await this.dbContext.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        return entity is null ? null : MapRunEntityToRecord(entity);
    }

    public async Task<WorkflowRunRecord?> FindRunByTriggerSourceAsync(
        Guid tenantId, string triggerSourceKind, Guid triggerSourceId, CancellationToken cancellationToken)
    {
        // Newest run wins when multiple share a trigger source — a submission can have one
        // SubmissionCompleted + many SubmissionUpdated, and the legacy single-run lookup expects
        // the most recent. Ordered by StartedAt because it's monotonic and indexed via PK
        // (Guid v7 timestamp prefix); FinishedAt is null for in-flight runs.
        var entity = await this.dbContext.WorkflowRuns
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.TriggerSourceKind == triggerSourceKind
                && r.TriggerSourceId == triggerSourceId)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : MapRunEntityToRecord(entity);
    }

    public async Task<IReadOnlyList<WorkflowRunRecord>> ListRunsByTriggerSourceAsync(
        Guid tenantId, string triggerSourceKind, Guid triggerSourceId, CancellationToken cancellationToken)
    {
        var entities = await this.dbContext.WorkflowRuns
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.TriggerSourceKind == triggerSourceKind
                && r.TriggerSourceId == triggerSourceId)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(MapRunEntityToRecord).ToList();
    }

    public async Task<WorkflowPagedResult<WorkflowRunRecord>> ListRunsAsync(
        WorkflowRunFilter filter, CancellationToken cancellationToken)
    {
        filter.Pagination.Validate();

        IQueryable<Entities.WorkflowRun> query = this.dbContext.WorkflowRuns
            .AsNoTracking()
            .Where(r => r.TenantId == filter.TenantId);

        if (filter.DefinitionId is { } definitionId)
        {
            query = query.Where(r => r.DefinitionId == definitionId);
        }
        if (filter.TriggerKind is not null)
        {
            query = query.Where(r => r.TriggerKind == filter.TriggerKind);
        }
        if (filter.TriggerSourceKind is not null)
        {
            query = query.Where(r => r.TriggerSourceKind == filter.TriggerSourceKind);
        }
        if (filter.TriggerSourceId is { } triggerSourceId)
        {
            query = query.Where(r => r.TriggerSourceId == triggerSourceId);
        }

        // Total first, slice second. Postgres uses ix_workflow_runs_tenant_id_created_at for the
        // sort+slice; the COUNT runs against the same WHERE without the ORDER BY/LIMIT, so the
        // planner picks the cheapest plan independently.
        var total = await query.LongCountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(filter.Pagination.Offset)
            .Take(filter.Pagination.Limit)
            .ToListAsync(cancellationToken);

        return new WorkflowPagedResult<WorkflowRunRecord>
        {
            Items = entities.Select(MapRunEntityToRecord).ToList(),
            Page = filter.Pagination.Page,
            Limit = filter.Pagination.Limit,
            TotalCount = total,
        };
    }

    public Task<int> CountChildRunsAsync(Guid parentRunId, CancellationToken cancellationToken)
    {
        // Hot path on RunWorkflow dispatch — index ix_workflow_runs_parent_run_id keeps this an
        // index-only count. WorkflowDispatcher.DispatchAsync calls SaveChangesAsync after each
        // successful AddRun, so by the time the cap is checked again for the same parent (next
        // step in the same batch, or a later batch), the previous child is already in the DB.
        // No local-overlay needed.
        return this.dbContext.WorkflowRuns
            .CountAsync(r => r.ParentRunId == parentRunId, cancellationToken);
    }

    // ===== Steps =====

    public void AddStep(WorkflowStepRecord step)
    {
        var entity = MapStepRecordToEntity(step);
        var now = DateTime.UtcNow;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        this.dbContext.WorkflowStepExecutions.Add(entity);
    }

    public void UpdateStep(WorkflowStepRecord step)
    {
        // Local-only lookup: caller has tracked-loaded the step earlier in this scope (claim
        // returns tracked entities; resumer / restarter call GetStepAsync first). With shared
        // batch scope this contract holds for every step in the batch — no Find fall-through.
        var entity = this.dbContext.WorkflowStepExecutions.Local.FirstOrDefault(e => e.Id == step.Id);
        if (entity is null) return;
        ApplyStepRecordToEntity(step, entity);
        entity.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<IReadOnlyList<WorkflowStepRecord>> GetStepsForRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var entities = await this.dbContext.WorkflowStepExecutions
            .AsNoTracking()
            .Where(s => s.RunId == runId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(MapStepEntityToRecord).ToList();
    }

    public async Task<WorkflowStepRecord?> GetStepAsync(Guid stepId, CancellationToken cancellationToken)
    {
        // Tracked load — resume / inspection callers may want to mutate via UpdateStep next.
        var entity = await this.dbContext.WorkflowStepExecutions
            .FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken);
        return entity is null ? null : MapStepEntityToRecord(entity);
    }

    public async Task<WorkflowStepRecord?> TryResumeWaitingStepAsync(
        Guid stepId,
        string outputPort,
        object? outputs,
        CancellationToken cancellationToken)
    {
        // Two-phase resume so we don't bypass EF value converters (consumers may layer column-
        // level converters such as PHI encryption on top of the engine's plain jsonb mapping;
        // raw SQL would skip them).
        //
        // 1) Atomic SQL guard: flip status from Waiting → Completed only if it's still Waiting.
        //    Returns 0 rows when someone else already resumed / the sweeper dead-lettered the
        //    step / the row doesn't exist — caller treats null as a 409.
        // 2) Tracked load: pull the row through EF and write the rest of the fields (output ports,
        //    outputs, completed_at, last_error) so the converters fire. EF will issue a follow-up
        //    UPDATE on SaveChanges that overlaps with the guard's status flip — that's fine, the
        //    flip is the same value either way.
        const string guardSql = """
            UPDATE workflow.workflow_step_executions
            SET status = {0}, updated_at = now()
            WHERE id = {1} AND status = {2}
            RETURNING id;
        """;

        var updatedIds = await this.dbContext.Database
            .SqlQueryRaw<Guid>(
                guardSql,
                StepExecutionStatus.Completed,
                stepId,
                StepExecutionStatus.Waiting)
            .ToListAsync(cancellationToken);

        if (updatedIds.Count == 0) return null;

        // Reuse the tracked entity if it's already in the change tracker (typical: caller did a
        // GetStepAsync to validate before calling resume). Sync its in-memory Status with what
        // the guard SQL just wrote so EF doesn't miss the change. Otherwise, pull a fresh tracked
        // row.
        var entity = this.dbContext.WorkflowStepExecutions.Local.FirstOrDefault(e => e.Id == stepId)
            ?? await this.dbContext.WorkflowStepExecutions
                .FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken);
        if (entity is null) return null;

        entity.Status = StepExecutionStatus.Completed;
        entity.OutputPort = outputPort;
        entity.Outputs = outputs is null ? entity.Outputs : JsonSerializer.SerializeToElement(outputs, WorkflowJsonOptions.Default);
        entity.CompletedAt = DateTime.UtcNow;
        entity.LastError = null;
        entity.UpdatedAt = DateTime.UtcNow;

        return MapStepEntityToRecord(entity);
    }

    // ===== Worker hot path =====

    public async Task<IReadOnlyList<WorkflowStepRecord>> ClaimPendingStepsAsync(
        int batchSize,
        WorkflowStepLane lane,
        CancellationToken cancellationToken)
    {
        // Atomically claim a batch of pending steps using FOR UPDATE SKIP LOCKED so multiple
        // workers can run concurrently without re-claiming each other's rows. Postgres-specific.
        // Two SQL variants: with or without the is_long_running filter. Branching by lane keeps
        // the Any-mode query identical to its pre-lane shape, so the planner re-uses cached
        // plans and the index scan stays optimal.
        var claimedIds = lane switch
        {
            WorkflowStepLane.Any => await this.ClaimPendingAnyAsync(batchSize, cancellationToken),
            WorkflowStepLane.FastOnly => await this.ClaimPendingByLaneAsync(batchSize, isLongRunning: false, cancellationToken),
            WorkflowStepLane.LongOnly => await this.ClaimPendingByLaneAsync(batchSize, isLongRunning: true, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unknown WorkflowStepLane value."),
        };

        if (claimedIds.Count == 0) return Array.Empty<WorkflowStepRecord>();

        // Tracked load — caller mutates and UpdateStep applies in place.
        var entities = await this.dbContext.WorkflowStepExecutions
            .Where(s => claimedIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        return entities.Select(MapStepEntityToRecord).ToList();
    }

    private Task<List<Guid>> ClaimPendingAnyAsync(int batchSize, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE workflow.workflow_step_executions
            SET status = {0}, attempt_count = attempt_count + 1, updated_at = now()
            WHERE id IN (
                SELECT id FROM workflow.workflow_step_executions
                WHERE status = {1} AND next_attempt_at <= now()
                ORDER BY next_attempt_at
                LIMIT {2}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id;
        """;
        return this.dbContext.Database
            .SqlQueryRaw<Guid>(sql, StepExecutionStatus.Running, StepExecutionStatus.Pending, batchSize)
            .ToListAsync(cancellationToken);
    }

    private Task<List<Guid>> ClaimPendingByLaneAsync(int batchSize, bool isLongRunning, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE workflow.workflow_step_executions
            SET status = {0}, attempt_count = attempt_count + 1, updated_at = now()
            WHERE id IN (
                SELECT id FROM workflow.workflow_step_executions
                WHERE status = {1} AND is_long_running = {2} AND next_attempt_at <= now()
                ORDER BY next_attempt_at
                LIMIT {3}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id;
        """;
        return this.dbContext.Database
            .SqlQueryRaw<Guid>(sql, StepExecutionStatus.Running, StepExecutionStatus.Pending, isLongRunning, batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountStepsForRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var saved = await this.dbContext.WorkflowStepExecutions.CountAsync(s => s.RunId == runId, cancellationToken);
        // Local-overlay must use ChangeTracker state — `CreatedAt == default` would never match
        // because AddStep stamps CreatedAt = utcNow before the entity goes into Local. Filter on
        // EntityState.Added so we only count freshly-staged-not-yet-flushed steps.
        var localPending = this.dbContext.ChangeTracker
            .Entries<WorkflowStepExecution>()
            .Count(e => e.State == EntityState.Added && e.Entity.RunId == runId);
        return saved + localPending;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetVisitsByNodeAsync(Guid runId, CancellationToken cancellationToken)
    {
        var saved = await this.dbContext.WorkflowStepExecutions
            .Where(s => s.RunId == runId)
            .GroupBy(s => s.NodeId)
            .Select(g => new { NodeId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var result = saved.ToDictionary(x => x.NodeId, x => x.Count);

        // Same EntityState.Added overlay as CountStepsForRunAsync — see explanation there.
        var pendingLocals = this.dbContext.ChangeTracker
            .Entries<WorkflowStepExecution>()
            .Where(e => e.State == EntityState.Added && e.Entity.RunId == runId)
            .Select(e => e.Entity);
        foreach (var local in pendingLocals)
        {
            result[local.NodeId] = result.GetValueOrDefault(local.NodeId) + 1;
        }

        return result;
    }

    public async Task<WorkflowRunStepStateSummary> GetStepStateSummaryAsync(
        Guid runId, Guid excludingStepId, CancellationToken cancellationToken)
    {
        // Single SQL roundtrip + Local-overlay reclassification. ClaimPendingStepsAsync writes
        // status='running' via raw SQL, then ApplyStepRecord flips entities to terminal states
        // purely in the change tracker — a pure DB query would see the stale 'running' value
        // for any step the worker just finished in this batch.
        // Strategy: pull (id, status) for every step in the run, then for each row let the local
        // entity (if tracked) override the DB status; also include local-only just-staged rows.
        // Three-way classification: PendingOrRunning (active progress), Waiting (parked on
        // external signal — drives run.Status = Suspended), Dead (terminal failure).
        static bool IsPendingOrRunning(string? status) =>
            status == StepExecutionStatus.Pending
            || status == StepExecutionStatus.Running;

        var dbItems = await this.dbContext.WorkflowStepExecutions
            .AsNoTracking()
            .Where(s => s.RunId == runId)
            .Select(s => new { s.Id, s.Status })
            .ToListAsync(cancellationToken);

        var localById = this.dbContext.WorkflowStepExecutions.Local
            .Where(s => s.RunId == runId)
            .ToDictionary(s => s.Id, s => s.Status);

        bool hasPendingOrRunning = false;
        bool hasWaiting = false;
        bool hasDead = false;

        foreach (var row in dbItems)
        {
            var status = localById.TryGetValue(row.Id, out var local) ? local : row.Status;
            // excludingStepId only excludes from PendingOrRunning — that filter exists to avoid
            // counting the just-finished step that may still be 'running' in stale DB rows.
            // Waiting / Dead categories include all steps so a just-suspended step contributes
            // to HasWaiting and a just-Dead step contributes to HasDead.
            if (row.Id != excludingStepId && IsPendingOrRunning(status))
            {
                hasPendingOrRunning = true;
            }
            if (status == StepExecutionStatus.Waiting)
            {
                hasWaiting = true;
            }
            if (status == StepExecutionStatus.Dead)
            {
                hasDead = true;
            }
            if (hasPendingOrRunning && hasWaiting && hasDead)
            {
                return new WorkflowRunStepStateSummary(true, true, true);
            }
        }

        // Local-only rows (AddStep'd this batch, not yet flushed) — same classification.
        var dbIds = dbItems.Select(r => r.Id).ToHashSet();
        foreach (var (id, status) in localById)
        {
            if (dbIds.Contains(id)) continue;
            if (id != excludingStepId && IsPendingOrRunning(status))
            {
                hasPendingOrRunning = true;
            }
            if (status == StepExecutionStatus.Waiting)
            {
                hasWaiting = true;
            }
            if (status == StepExecutionStatus.Dead)
            {
                hasDead = true;
            }
            if (hasPendingOrRunning && hasWaiting && hasDead)
            {
                break;
            }
        }

        return new WorkflowRunStepStateSummary(hasPendingOrRunning, hasWaiting, hasDead);
    }

    // ===== Suspend / timeout =====

    public Task<int> ReleaseClaimedStepsAsync(
        IReadOnlyList<Guid> stepIds,
        CancellationToken cancellationToken)
    {
        if (stepIds.Count == 0) return Task.FromResult(0);

        // Guarded SQL: only revert rows that are still in 'running' (those we claimed but
        // didn't finish executing). Rows already moved on by a concurrent cancel / external
        // mutation stay where they are — `status='running'` filter excludes them naturally.
        // attempt_count -= 1 reverses the bump that ClaimPendingStepsAsync applied "on credit"
        // when it claimed; the upcoming retry isn't penalised for a non-attempt.
        const string sql = """
            UPDATE workflow.workflow_step_executions
            SET status = {0}, attempt_count = attempt_count - 1, updated_at = now()
            WHERE id = ANY({1})
              AND status = {2};
        """;

        return this.dbContext.Database.ExecuteSqlRawAsync(
            sql,
            new object[]
            {
                StepExecutionStatus.Pending,
                stepIds.ToArray(),
                StepExecutionStatus.Running,
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowStepRecord>> ClaimExpiredWaitingStepsAsync(
        int limit,
        WorkflowStepLane lane,
        CancellationToken cancellationToken)
    {
        // Same atomic claim shape as ClaimPendingStepsAsync — FOR UPDATE SKIP LOCKED guarantees
        // no two workers see the same expired-waiting step. Status flips Waiting → Running in
        // one statement; the caller's HandleTimeoutGracefullyAsync drives the timeout outcome
        // and ApplyResultAsync moves it to Completed/Dead like a regular step termination.
        // attempt_count is NOT incremented — this isn't a retry, it's a one-shot timeout fire.
        // Lane filter mirrors ClaimPendingStepsAsync so OnTimeoutAsync (which can also be slow)
        // runs on the matching pool — long-running's timeout doesn't block fast workers.
        var claimedIds = lane switch
        {
            WorkflowStepLane.Any => await this.ClaimExpiredAnyAsync(limit, cancellationToken),
            WorkflowStepLane.FastOnly => await this.ClaimExpiredByLaneAsync(limit, isLongRunning: false, cancellationToken),
            WorkflowStepLane.LongOnly => await this.ClaimExpiredByLaneAsync(limit, isLongRunning: true, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unknown WorkflowStepLane value."),
        };

        if (claimedIds.Count == 0) return Array.Empty<WorkflowStepRecord>();

        // Tracked load — caller mutates and UpdateStep applies in place.
        var entities = await this.dbContext.WorkflowStepExecutions
            .Where(s => claimedIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        return entities.Select(MapStepEntityToRecord).ToList();
    }

    private Task<List<Guid>> ClaimExpiredAnyAsync(int limit, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE workflow.workflow_step_executions
            SET status = {0}, updated_at = now()
            WHERE id IN (
                SELECT id FROM workflow.workflow_step_executions
                WHERE status = {1} AND next_attempt_at <= now()
                ORDER BY next_attempt_at
                LIMIT {2}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id;
        """;
        return this.dbContext.Database
            .SqlQueryRaw<Guid>(sql, StepExecutionStatus.Running, StepExecutionStatus.Waiting, limit)
            .ToListAsync(cancellationToken);
    }

    private Task<List<Guid>> ClaimExpiredByLaneAsync(int limit, bool isLongRunning, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE workflow.workflow_step_executions
            SET status = {0}, updated_at = now()
            WHERE id IN (
                SELECT id FROM workflow.workflow_step_executions
                WHERE status = {1} AND is_long_running = {2} AND next_attempt_at <= now()
                ORDER BY next_attempt_at
                LIMIT {3}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id;
        """;
        return this.dbContext.Database
            .SqlQueryRaw<Guid>(sql, StepExecutionStatus.Running, StepExecutionStatus.Waiting, isLongRunning, limit)
            .ToListAsync(cancellationToken);
    }

    // ===== Purge =====

    public Task<int> PurgeFinishedRunsAsync(
        DateTime olderThan,
        int limit,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = this.dbContext.WorkflowRuns
            .Where(r => (r.Status == WorkflowRunStatus.Completed || r.Status == WorkflowRunStatus.Failed)
                        && r.FinishedAt != null
                        && r.FinishedAt < olderThan);

        if (tenantId.HasValue)
        {
            var tid = tenantId.Value;
            query = query.Where(r => r.TenantId == tid);
        }

        // ExecuteDeleteAsync with OrderBy+Take generates DELETE WHERE id IN (SELECT ... LIMIT N).
        // Steps cascade via the FK on workflow_step_executions.run_id.
        return query
            .OrderBy(r => r.FinishedAt)
            .Take(limit)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> PurgeAllForTenantAsync(
        Guid tenantId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return this.dbContext.WorkflowRuns
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.StartedAt)
            .Take(limit)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> PurgeRunsByDefinitionAsync(
        Guid tenantId,
        Guid definitionId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Tenant-scoped on purpose — defence in depth. Even though (tenantId, definitionId) is
        // 1:1 with the definition row, a malformed call shouldn't be able to wipe runs from a
        // different tenant just by guessing a definitionId.
        return this.dbContext.WorkflowRuns
            .Where(r => r.TenantId == tenantId && r.DefinitionId == definitionId)
            .OrderBy(r => r.CreatedAt)
            .Take(limit)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> PurgeStaleRunningRunsAsync(
        DateTime olderThan,
        int limit,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        // Status='running' here strictly means "actively progressing" — runs parked on an
        // external signal (Approve / Delay / RunWorkflow wait-for-completion) carry the
        // dedicated 'suspended' status set by CheckRunCompletionAsync, so they're naturally
        // excluded from this scan. No NOT EXISTS subquery needed.
        var query = this.dbContext.WorkflowRuns
            .Where(r => r.Status == WorkflowRunStatus.Running && r.UpdatedAt < olderThan);

        if (tenantId.HasValue)
        {
            var tid = tenantId.Value;
            query = query.Where(r => r.TenantId == tid);
        }

        return query
            .OrderBy(r => r.UpdatedAt)
            .Take(limit)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // ===== Atomic flush =====

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        this.dbContext.SaveChangesAsync(cancellationToken);

    public void DiscardPendingChanges()
    {
        // Detach every entry that has dirty state. Unchanged entries (already-flushed reads
        // from earlier in the same scope) stay tracked so subsequent code paths benefit from
        // the cache rather than re-loading. ToList() materialises before mutating, since
        // changing entry.State during iteration would invalidate the enumerator.
        foreach (var entry in this.dbContext.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    // ===== Mapping =====

    private static PluginWorkflowDefinition MapDefinition(Entities.WorkflowDefinition entity)
    {
        var graph = JsonSerializer.Deserialize<WorkflowGraph>(entity.Graph, WorkflowJsonOptions.Default) ?? new WorkflowGraph();
        return new PluginWorkflowDefinition
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            OwnerKind = entity.OwnerKind,
            OwnerId = entity.OwnerId,
            TriggerKind = entity.TriggerKind,
            DisplayName = entity.DisplayName,
            Graph = graph,
        };
    }

    private static WorkflowRun MapRunRecordToEntity(WorkflowRunRecord r) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        DefinitionId = r.DefinitionId,
        TriggerKind = r.TriggerKind,
        TriggerSourceKind = r.TriggerSourceKind,
        TriggerSourceId = r.TriggerSourceId,
        IsDryRun = r.IsDryRun,
        Name = r.Name,
        ActorUserId = r.ActorUserId,
        WorkflowSnapshot = r.WorkflowSnapshot,
        StaticContext = r.StaticContext,
        StepsOutputs = r.StepsOutputs,
        Status = r.Status,
        AbortReason = r.AbortReason,
        StartedAt = r.StartedAt,
        FinishedAt = r.FinishedAt,
        ReturnValue = r.ReturnValue,
        NestingLevel = r.NestingLevel,
        ParentRunId = r.ParentRunId,
        ParentStepId = r.ParentStepId,
    };

    private static void ApplyRunRecordToEntity(WorkflowRunRecord r, WorkflowRun e)
    {
        e.StepsOutputs = r.StepsOutputs;
        e.Status = r.Status;
        e.AbortReason = r.AbortReason;
        e.FinishedAt = r.FinishedAt;
        e.ReturnValue = r.ReturnValue;
        // Name is mutable mid-run via the SetRunName action; apply here so UpdateRun picks up
        // changes the action made on the tracked record.
        e.Name = r.Name;
    }

    private static WorkflowRunRecord MapRunEntityToRecord(WorkflowRun e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        DefinitionId = e.DefinitionId,
        TriggerKind = e.TriggerKind,
        TriggerSourceKind = e.TriggerSourceKind,
        TriggerSourceId = e.TriggerSourceId,
        IsDryRun = e.IsDryRun,
        Name = e.Name,
        ActorUserId = e.ActorUserId,
        WorkflowSnapshot = e.WorkflowSnapshot,
        StaticContext = e.StaticContext,
        StepsOutputs = e.StepsOutputs,
        Status = e.Status,
        AbortReason = e.AbortReason,
        CreatedAt = e.CreatedAt,
        StartedAt = e.StartedAt,
        FinishedAt = e.FinishedAt,
        ReturnValue = e.ReturnValue,
        NestingLevel = e.NestingLevel,
        ParentRunId = e.ParentRunId,
        ParentStepId = e.ParentStepId,
    };

    private static WorkflowStepExecution MapStepRecordToEntity(WorkflowStepRecord r) => new()
    {
        Id = r.Id,
        RunId = r.RunId,
        TenantId = r.TenantId,
        NodeId = r.NodeId,
        Kind = r.Kind,
        Name = r.Name,
        PredecessorExecutionId = r.PredecessorExecutionId,
        TriggerPort = r.TriggerPort,
        ResolvedConfig = r.ResolvedConfig,
        IsLongRunning = r.IsLongRunning,
        Status = r.Status,
        OutputPort = r.OutputPort,
        AttemptCount = r.AttemptCount,
        NextAttemptAt = r.NextAttemptAt,
        CompletedAt = r.CompletedAt,
        LastError = r.LastError,
        Outputs = r.Outputs,
    };

    private static void ApplyStepRecordToEntity(WorkflowStepRecord r, WorkflowStepExecution e)
    {
        e.Status = r.Status;
        e.OutputPort = r.OutputPort;
        e.AttemptCount = r.AttemptCount;
        e.NextAttemptAt = r.NextAttemptAt;
        e.CompletedAt = r.CompletedAt;
        e.LastError = r.LastError;
        e.Outputs = r.Outputs;
    }

    private static WorkflowStepRecord MapStepEntityToRecord(WorkflowStepExecution e) => new()
    {
        Id = e.Id,
        RunId = e.RunId,
        TenantId = e.TenantId,
        NodeId = e.NodeId,
        Kind = e.Kind,
        Name = e.Name,
        PredecessorExecutionId = e.PredecessorExecutionId,
        TriggerPort = e.TriggerPort,
        ResolvedConfig = e.ResolvedConfig,
        IsLongRunning = e.IsLongRunning,
        Status = e.Status,
        OutputPort = e.OutputPort,
        AttemptCount = e.AttemptCount,
        NextAttemptAt = e.NextAttemptAt,
        CompletedAt = e.CompletedAt,
        LastError = e.LastError,
        Outputs = e.Outputs,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };
}
