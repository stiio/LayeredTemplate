using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Storage.EFCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PluginWorkflowDefinition = LayeredTemplate.Plugins.Workflow.Abstractions.Models.WorkflowDefinition;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// EF Core impl of <see cref="IWorkflowStore"/>. Postgres-specific only in the
/// <see cref="ClaimPendingStepIdsAsync"/> raw SQL (FOR UPDATE SKIP LOCKED + RETURNING).
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
        JsonElement? globals, string? displayName, CancellationToken cancellationToken)
    {
        if (globals is { } incomingGlobals)
        {
            WorkflowGlobals.EnsureValid(incomingGlobals);
        }

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
                Globals = SerializeGlobals(globals),
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
            // Same tri-state for globals: null = leave as-is, explicit {} = clear (stored NULL).
            if (globals is { } updatedGlobals)
            {
                existing.Globals = SerializeGlobals(updatedGlobals);
            }
            existing.UpdatedAt = now;
        }
    }

    /// <summary>Empty object normalizes to NULL so "no globals" has one canonical representation.</summary>
    private static string? SerializeGlobals(JsonElement? globals) =>
        globals is { } g && g.EnumerateObject().Any() ? g.GetRawText() : null;

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
        // is tracked in this scope before mutating (via GetRunAsync). A Local miss means the
        // caller broke that contract and the mutation would be silently lost; fail fast
        // instead of round-tripping stale state.
        var entity = this.dbContext.WorkflowRuns.Local.FirstOrDefault(e => e.Id == run.Id);
        if (entity is null)
        {
            throw new InvalidOperationException(
                $"UpdateRun: run {run.Id} is not tracked in this scope. Load it via GetRunAsync " +
                "on the same store before mutating — otherwise the update would be silently dropped.");
        }
        ApplyRunRecordToEntity(run, entity);
        entity.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<WorkflowRunRecord?> GetRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        // Tracked load — worker mutates fields and we want UpdateRun to see the same instance.
        var entity = await this.dbContext.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        return entity is null ? null : MapRunEntityToRecord(entity);
    }

    public async Task<WorkflowPagedResult<WorkflowRunRecord>> ListRunsAsync(
        WorkflowRunFilter filter, CancellationToken cancellationToken)
    {
        filter.Pagination.Validate();

        IQueryable<Entities.WorkflowRun> query = this.dbContext.WorkflowRuns.AsNoTracking();

        // Null tenant = explicit admin-wide listing (see WorkflowRunFilter.TenantId doc).
        if (filter.TenantId is { } tenantId)
        {
            query = query.Where(r => r.TenantId == tenantId);
        }
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
        if (filter.IsDryRun is { } isDryRun)
        {
            query = query.Where(r => r.IsDryRun == isDryRun);
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

    public async Task<int> CountChildRunsAsync(Guid parentRunId, CancellationToken cancellationToken)
    {
        // Hot path on RunWorkflow dispatch — index ix_workflow_runs_parent_run_id keeps this an
        // index-only count.
        var saved = await this.dbContext.WorkflowRuns
            .CountAsync(r => r.ParentRunId == parentRunId, cancellationToken);
        // Local overlay, same EntityState.Added pattern as CountStepsForRunAsync: the RunWorkflow
        // action dispatches with flush:false, staging the child on the step's scoped context so
        // it commits atomically with the dispatching step's transition. A cap check that runs
        // before that flush must still see the staged child, or a same-flush sequence could
        // overshoot MaxSubRunsPerRun.
        var localPending = this.dbContext.ChangeTracker
            .Entries<WorkflowRun>()
            .Count(e => e.State == EntityState.Added && e.Entity.ParentRunId == parentRunId);
        return saved + localPending;
    }

    public Task<bool> AnyRunsForDefinitionAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        // No tenant filter on purpose: system-workflow runs live under workspace tenants, not the
        // definition's tenant (ADR-028 §4). The question is "did this definition ever run anywhere?"
        return this.dbContext.WorkflowRuns
            .AsNoTracking()
            .AnyAsync(r => r.DefinitionId == definitionId, cancellationToken);
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
        // Local-only lookup: caller has tracked-loaded the step earlier in this scope (the
        // per-step worker scope and the resumer both call GetStepAsync first). A Local miss
        // means the caller broke that contract and the mutation would be silently lost; fail
        // fast instead of round-tripping stale state.
        var entity = this.dbContext.WorkflowStepExecutions.Local.FirstOrDefault(e => e.Id == step.Id);
        if (entity is null)
        {
            throw new InvalidOperationException(
                $"UpdateStep: step {step.Id} is not tracked in this scope. Load it via GetStepAsync " +
                "(or a claim) on the same store before mutating — otherwise the update would be silently dropped.");
        }
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
        //
        // Atomicity: the guard enlists in the scope's ambient transaction when one is open — the
        // resumer wraps the whole resume in BeginTransactionAsync, so the flip, the field writes,
        // and the caller-staged fan-out commit (and roll back) together. The guard's row lock is
        // then held until that commit: a competing resume's guard blocks briefly and loses with
        // 0 rows; the timeout sweeper's FOR UPDATE SKIP LOCKED claim skips the locked row.
        // Without an ambient transaction (bare store usage) the flip auto-commits as before.
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

    // ===== Bookmarks (generic signal-wait) =====

    public void AddBookmarks(WorkflowStepRecord step, IReadOnlyList<WorkflowBookmarkRegistration> registrations)
    {
        var now = DateTime.UtcNow;
        foreach (var reg in registrations)
        {
            this.dbContext.WorkflowBookmarks.Add(new Entities.WorkflowBookmark
            {
                TenantId = step.TenantId,
                RunId = step.RunId,
                StepId = step.Id,
                CorrelationKey = reg.CorrelationKey,
                ResumePort = reg.ResumePort,
                CreatedAt = now,
            });
        }
    }

    public async Task<IReadOnlyList<WorkflowBookmarkRecord>> FindBookmarksAsync(
        Guid tenantId, string correlationKey, CancellationToken cancellationToken)
    {
        // Tenant-scoped lookup — mandatory isolation. ix_workflow_bookmark_tenant_id_correlation_key
        // serves this directly. AsNoTracking: the signaler resumes via the resumer (its own tracked
        // step load) and deletes via a set-based DELETE, so it never mutates these instances.
        var entities = await this.dbContext.WorkflowBookmarks
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.CorrelationKey == correlationKey)
            .ToListAsync(cancellationToken);
        return entities.Select(MapBookmarkEntityToRecord).ToList();
    }

    public Task<int> DeleteBookmarksAsync(IReadOnlyList<Guid> bookmarkIds, CancellationToken cancellationToken)
    {
        if (bookmarkIds.Count == 0) return Task.FromResult(0);

        // Set-based delete by id — eager cleanup after a resume / stale outcome. ExecuteDeleteAsync
        // bypasses the change tracker; that's fine, these rows aren't tracked (FindBookmarksAsync
        // is AsNoTracking).
        return this.dbContext.WorkflowBookmarks
            .Where(b => bookmarkIds.Contains(b.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> SweepResolvedBookmarksAsync(int limit, CancellationToken cancellationToken)
    {
        // Reconciliation backstop. A bookmark is only valid while its step is Waiting; the moment
        // the step leaves Waiting (resumed / timed-out / dead-lettered / cancelled) by ANY path,
        // the bookmark is garbage. One set-based DELETE … USING the step table, bounded by LIMIT
        // so a huge backlog drains in cadence-sized chunks. Set-based (not EF query) because the
        // <> comparison against the joined step status has no clean LINQ form here.
        const string sql = """
            DELETE FROM workflow.workflow_bookmark b
            USING workflow.workflow_step_executions s
            WHERE b.step_id = s.id
              AND s.status <> {0}
              AND b.id IN (
                  SELECT b2.id FROM workflow.workflow_bookmark b2
                  JOIN workflow.workflow_step_executions s2 ON b2.step_id = s2.id
                  WHERE s2.status <> {0}
                  LIMIT {1}
              );
        """;
        return this.dbContext.Database.ExecuteSqlRawAsync(
            sql,
            new object[] { StepExecutionStatus.Waiting, limit },
            cancellationToken);
    }

    // ===== Worker hot path =====

    public async Task<IReadOnlyList<Guid>> ClaimPendingStepIdsAsync(
        int batchSize,
        WorkflowStepLane lane,
        CancellationToken cancellationToken)
    {
        // Atomically claim a batch of pending steps using FOR UPDATE SKIP LOCKED so multiple
        // workers can run concurrently without re-claiming each other's rows. Postgres-specific.
        // Two SQL variants: with or without the is_long_running filter. Branching by lane keeps
        // the Any-mode query identical to its pre-lane shape, so the planner re-uses cached
        // plans and the index scan stays optimal. Ids only — the worker loads each step through
        // its own per-step scope's GetStepAsync; the claim UPDATE commits immediately (raw SQL),
        // so the claim survives this scope's disposal.
        return lane switch
        {
            WorkflowStepLane.Any => await this.ClaimPendingAnyAsync(batchSize, cancellationToken),
            WorkflowStepLane.FastOnly => await this.ClaimPendingByLaneAsync(batchSize, isLongRunning: false, cancellationToken),
            WorkflowStepLane.LongOnly => await this.ClaimPendingByLaneAsync(batchSize, isLongRunning: true, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unknown WorkflowStepLane value."),
        };
    }

    private Task<List<Guid>> ClaimPendingAnyAsync(int batchSize, CancellationToken cancellationToken)
    {
        // started_at stamps the moment THIS attempt began running (overwritten per retry claim)
        // — CompletedAt - StartedAt is the honest execution duration even under worker backlog,
        // where next_attempt_at-based math would count queue wait as work.
        const string sql = """
            UPDATE workflow.workflow_step_executions
            SET status = {0}, attempt_count = attempt_count + 1, started_at = now(), updated_at = now()
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
            SET status = {0}, attempt_count = attempt_count + 1, started_at = now(), updated_at = now()
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

    public async Task<int> CountVisitsForNodeAsync(Guid runId, string nodeId, CancellationToken cancellationToken)
    {
        // Targeted count for the fan-out's MaxVisitsPerNode check — the caller only ever needs
        // the enqueue TARGET's count. The previous shape (GROUP BY node_id over every step of
        // the run, histogram shipped to the client per enqueue) cost O(steps) per step and
        // O(N²) per run. ix_workflow_step_executions_run_id_created_at serves the run_id
        // prefix; no rows materialize client-side.
        var saved = await this.dbContext.WorkflowStepExecutions
            .CountAsync(s => s.RunId == runId && s.NodeId == nodeId, cancellationToken);
        // Same EntityState.Added overlay as CountStepsForRunAsync — count steps staged in this
        // batch but not yet flushed, narrowed to the one node we're checking.
        var localPending = this.dbContext.ChangeTracker
            .Entries<WorkflowStepExecution>()
            .Count(e => e.State == EntityState.Added
                && e.Entity.RunId == runId
                && e.Entity.NodeId == nodeId);
        return saved + localPending;
    }

    public async Task<WorkflowRunStepStateSummary> GetStepStateSummaryAsync(
        Guid runId, Guid excludingStepId, CancellationToken cancellationToken)
    {
        // Runs after EVERY step completion (CheckRunCompletionAsync), so the cost must not scale
        // with run history. ClaimPendingStepIdsAsync writes status='running' via raw SQL, then
        // ApplyStepRecord flips entities to terminal states purely in the change tracker — the
        // DB status is stale exactly for the rows this scope tracks. Strategy therefore:
        //   1. Classify tracked rows (the current step + anything its scope staged — a handful)
        //      from their in-memory statuses.
        //   2. Aggregate the untracked remainder SERVER-SIDE into three booleans in one
        //      roundtrip — no per-completion transfer of the run's step list (the previous
        //      shape pulled (id, status) for every step: O(steps) per completion, O(N²) per run).
        // Three-way classification: PendingOrRunning (active progress), Waiting (parked on
        // external signal — drives run.Status = Suspended), Dead (terminal failure).
        // excludingStepId only excludes from PendingOrRunning — that filter exists to avoid
        // counting the just-finished step whose DB row may still say 'running'. Waiting / Dead
        // include all steps so a just-suspended step contributes to HasWaiting and a just-Dead
        // step contributes to HasDead.
        static bool IsPendingOrRunning(string? status) =>
            status == StepExecutionStatus.Pending
            || status == StepExecutionStatus.Running;

        var tracked = this.dbContext.WorkflowStepExecutions.Local
            .Where(s => s.RunId == runId)
            .ToList();

        bool hasPendingOrRunning = false;
        bool hasWaiting = false;
        bool hasDead = false;

        foreach (var step in tracked)
        {
            if (step.Id != excludingStepId && IsPendingOrRunning(step.Status))
            {
                hasPendingOrRunning = true;
            }
            if (step.Status == StepExecutionStatus.Waiting)
            {
                hasWaiting = true;
            }
            if (step.Status == StepExecutionStatus.Dead)
            {
                hasDead = true;
            }
        }

        // All three already proven by tracked rows — the DB can't subtract, only add.
        if (hasPendingOrRunning && hasWaiting && hasDead)
        {
            return new WorkflowRunStepStateSummary(true, true, true);
        }

        // bool_or aggregates the untracked remainder without shipping rows; COALESCE covers the
        // zero-row case (an aggregate over nothing yields NULL). Tracked ids are excluded — their
        // in-memory classification above is authoritative — and `<> ALL('{}')` is true, so an
        // empty tracked set degrades to a plain run-wide aggregate. Quoted aliases pin the exact
        // casing EF's unmapped-type materializer matches properties by. ToListAsync on purpose:
        // it runs the raw SQL verbatim, while Single/FirstAsync would compose (wrap in a
        // subquery + LIMIT) — an aggregate without GROUP BY always yields exactly one row anyway.
        const string sql = """
            SELECT
                COALESCE(bool_or(status IN ({0}, {1}) AND id <> {2}), FALSE) AS "HasOngoing",
                COALESCE(bool_or(status = {3}), FALSE) AS "HasWaiting",
                COALESCE(bool_or(status = {4}), FALSE) AS "HasDead"
            FROM workflow.workflow_step_executions
            WHERE run_id = {5} AND id <> ALL({6});
        """;

        var dbFlags = (await this.dbContext.Database
            .SqlQueryRaw<StepStateFlagsRow>(
                sql,
                StepExecutionStatus.Pending,
                StepExecutionStatus.Running,
                excludingStepId,
                StepExecutionStatus.Waiting,
                StepExecutionStatus.Dead,
                runId,
                tracked.Select(s => s.Id).ToArray())
            .ToListAsync(cancellationToken))[0];

        return new WorkflowRunStepStateSummary(
            hasPendingOrRunning || dbFlags.HasOngoing,
            hasWaiting || dbFlags.HasWaiting,
            hasDead || dbFlags.HasDead);
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
        // attempt_count -= 1 reverses the bump that ClaimPendingStepIdsAsync applied "on credit"
        // when it claimed; the upcoming retry isn't penalised for a non-attempt. started_at is
        // refunded with it — the attempt never ran, a stale start stamp on a pending row would
        // only confuse duration math.
        const string sql = """
            UPDATE workflow.workflow_step_executions
            SET status = {0}, attempt_count = attempt_count - 1, started_at = NULL, updated_at = now()
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

    public async Task<IReadOnlyList<Guid>> ReclaimStuckRunningStepIdsAsync(
        DateTime olderThan,
        int limit,
        CancellationToken cancellationToken)
    {
        // Crash recovery — the only path that touches 'running' rows by age. updated_at is the
        // liveness marker: every legitimate transition (claim, timeout flip, outcome write)
        // stamps it, so an old stamp means the executing worker died without a catch block ever
        // running. Back to 'pending' with next_attempt_at = now: the regular claim machinery
        // (and MaxAttempts, since the crashed attempt stays counted) takes it from there.
        // Hits the partial ix_workflow_step_executions_running_updated_at index.
        const string sql = """
            UPDATE workflow.workflow_step_executions
            SET status = {0}, next_attempt_at = now(), started_at = NULL, updated_at = now()
            WHERE id IN (
                SELECT id FROM workflow.workflow_step_executions
                WHERE status = {1} AND updated_at < {2}
                ORDER BY updated_at
                LIMIT {3}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id;
        """;
        return await this.dbContext.Database
            .SqlQueryRaw<Guid>(sql, StepExecutionStatus.Pending, StepExecutionStatus.Running, olderThan, limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ClaimExpiredWaitingStepIdsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        // Same atomic claim shape as ClaimPendingStepIdsAsync — FOR UPDATE SKIP LOCKED guarantees
        // no two sweep passes see the same expired-waiting step. Status flips Waiting → Running
        // in one statement; the caller's HandleTimeoutGracefullyAsync drives the timeout outcome
        // and ApplyResultAsync moves it to Completed/Dead like a regular step termination.
        // attempt_count is NOT incremented — this isn't a retry, it's a one-shot timeout fire.
        // No lane filter: timeouts are swept by the engine's single maintenance loop regardless
        // of the step's lane — OnStepTimedOutAsync hooks are quick decision code, not action
        // bodies, so lane isolation buys nothing here. Ids only: each expired step is handled
        // in its own per-step scope, which tracked-loads it via GetStepAsync.
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
        return await this.dbContext.Database
            .SqlQueryRaw<Guid>(sql, StepExecutionStatus.Running, StepExecutionStatus.Waiting, limit)
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
        Guid definitionId,
        int limit,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        // Null tenant = the system-definition case: the definition lives under the system
        // tenant while its runs execute under each operator's workspace tenant (same reasoning
        // as AnyRunsForDefinitionAsync), so only a cross-tenant sweep can clear them. A non-null
        // tenant keeps the defence-in-depth scoping for owner-tenant definitions — a malformed
        // call can't wipe another tenant's runs by guessing a definitionId.
        var query = this.dbContext.WorkflowRuns
            .Where(r => r.DefinitionId == definitionId);
        if (tenantId is { } tid)
        {
            query = query.Where(r => r.TenantId == tid);
        }

        return query
            .OrderBy(r => r.CreatedAt)
            .Take(limit)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Diagnostic abort reason stamped on stale-failed runs — greppable in dashboards.</summary>
    internal const string StaleAbortReason = "stale: run showed no activity within the stale-running retention window";

    public Task<int> FailStaleRunningRunsAsync(
        DateTime olderThan,
        int limit,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        // Status='running' here strictly means "actively progressing" — runs parked on an
        // external signal (Delay / WaitSignal / RunWorkflow wait-for-completion) carry the
        // dedicated 'suspended' status set by CheckRunCompletionAsync, so they're naturally
        // excluded from this scan. No NOT EXISTS subquery needed.
        var query = this.dbContext.WorkflowRuns
            .Where(r => r.Status == WorkflowRunStatus.Running && r.UpdatedAt < olderThan);

        if (tenantId.HasValue)
        {
            var tid = tenantId.Value;
            query = query.Where(r => r.TenantId == tid);
        }

        // Two-phase reaping: flip to Failed with a diagnostic abort_reason instead of deleting —
        // the run (and its step history) stays inspectable until the FINISHED purge removes it
        // like any other failed run. Set-based ExecuteUpdate goes through EF's translation, so
        // the abort_reason constant passes the protected-column converter like a tracked write.
        var now = DateTime.UtcNow;
        return query
            .OrderBy(r => r.UpdatedAt)
            .Take(limit)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Status, WorkflowRunStatus.Failed)
                    .SetProperty(r => r.AbortReason, StaleAbortReason)
                    .SetProperty(r => r.FinishedAt, now)
                    .SetProperty(r => r.UpdatedAt, now),
                cancellationToken);
    }

    // ===== Atomic flush =====

    public async Task<IWorkflowStoreTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        // Ambient-transaction detection: a nested caller (the resumer's chain-unwind case)
        // participates in the outer transaction instead of opening its own — Npgsql forbids
        // real nesting on one connection anyway.
        if (this.dbContext.Database.CurrentTransaction is not null) return null;

        var transaction = await this.dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EfWorkflowStoreTransaction(transaction);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        this.dbContext.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// Thin adapter from EF's <see cref="IDbContextTransaction"/> to the storage-agnostic
    /// handle. Disposing without commit rolls back (EF semantics).
    /// </summary>
    private sealed class EfWorkflowStoreTransaction : IWorkflowStoreTransaction
    {
        private readonly IDbContextTransaction transaction;

        public EfWorkflowStoreTransaction(IDbContextTransaction transaction) => this.transaction = transaction;

        public Task CommitAsync(CancellationToken cancellationToken) => this.transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => this.transaction.DisposeAsync();
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
            Globals = entity.Globals is { } globalsJson
                ? JsonSerializer.Deserialize<JsonElement>(globalsJson)
                : null,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
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

    private static WorkflowBookmarkRecord MapBookmarkEntityToRecord(WorkflowBookmark e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        RunId = e.RunId,
        StepId = e.StepId,
        CorrelationKey = e.CorrelationKey,
        ResumePort = e.ResumePort,
        CreatedAt = e.CreatedAt,
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
        StartedAt = r.StartedAt,
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
        e.StartedAt = r.StartedAt;
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
        StartedAt = e.StartedAt,
        CompletedAt = e.CompletedAt,
        LastError = e.LastError,
        Outputs = e.Outputs,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };
}

/// <summary>
/// Row shape for the aggregated step-state probe in
/// <see cref="EfCoreWorkflowStore.GetStepStateSummaryAsync"/>. Deliberately top-level — EF's
/// ad-hoc <c>SqlQuery</c> materialization refuses nested CLR types.
/// </summary>
internal sealed class StepStateFlagsRow
{
    public bool HasOngoing { get; set; }

    public bool HasWaiting { get; set; }

    public bool HasDead { get; set; }
}
