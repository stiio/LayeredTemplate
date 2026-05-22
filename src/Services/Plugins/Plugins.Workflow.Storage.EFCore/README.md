# Plugins.Workflow.Storage.EFCore

EF Core / **Postgres-only** backend for the workflow engine. Implements `IWorkflowStore` (and
transitively `IWorkflowReadStore` + `IWorkflowRetentionStore`) against a plugin-owned
`DbContext` that lives in its own `workflow` schema with its own migration history table —
fully decoupled from any consumer-side EF model.

The engine itself doesn't care which backend serves it. This plugin just happens to be the
Postgres one. To run on Mongo / SQL Server / DynamoDB, implement `IWorkflowStore` from scratch
in a sibling project and swap `AddEfCoreStorage(...)` for `AddXxxStorage(...)` in DI.

## Contents

- [Folder map](#folder-map)
- [Setup](#setup)
- [Schema](#schema)
  - [Indexes](#indexes)
  - [Foreign keys](#foreign-keys)
- [Migrations](#migrations)
- [PHI encryption](#phi-encryption)
- [Capacity diagnosis](#capacity-diagnosis)
  - [Pool sizing](#pool-sizing)
  - [Mistagged actions](#mistagged-actions)
  - [Worker health](#worker-health)
  - [Retry pressure](#retry-pressure)
  - [Run-level inspection](#run-level-inspection)
- [Operational SQL](#operational-sql)
- [Why "Postgres-only"](#why-postgres-only)
- [What's NOT here](#whats-not-here)

## Folder map

```
Entities/                          — EF entities the engine persists.
├── WorkflowDefinition.cs          — Stored graph + (TenantId, OwnerKind, OwnerId, TriggerKind)
│                                    lookup key + optional DisplayName.
├── WorkflowRun.cs                  — A single run: snapshot, static context (JSON-typed),
│                                    accumulated step outputs (JSON-typed), status, trigger
│                                    source, parent-run/step linkage, protection version,
│                                    xmin concurrency token.
├── WorkflowStepExecution.cs        — Per-step state: kind, resolved config (JSON-typed),
│                                    outputs (JSON-typed), is_long_running lane flag,
│                                    protection version, xmin concurrency token.
└── IHaveProtectedData.cs           — Marker interface implemented by entities whose protected
                                    columns get stamped with the active key version.

Configurations/                     — IEntityTypeConfiguration<T> for each entity. Every column,
                                    index, PK, FK is named explicitly (no convention package);
                                    the schema contract is locked in here.

Migrations/                         — EF migrations. The plugin owns its own history table
                                    (workflow.__EFMigrationsHistory) so consumer migrations
                                    don't entangle.

EfCoreWorkflowStore.cs              — IWorkflowStore implementation. Postgres-specific bits:
                                    raw FOR UPDATE SKIP LOCKED claim queries, ANY($1) parameter
                                    arrays, ExecuteSqlRawAsync for atomic abort/release SQL.
EfCoreWorkflowStorageMigrator.cs    — Public IWorkflowStorageMigrator surface for explicit
                                    migration runs.
WorkflowMigrationHostedService.cs   — Optional hosted service that applies pending migrations
                                    on startup under a Postgres advisory lock so multi-instance
                                    starts don't race.
WorkflowDbContext.cs                — Internal DbContext. Owns `workflow` schema. Configures
                                    JSON / string protected-column converters and the xmin
                                    concurrency token mapping.
WorkflowProtectedStringConverter.cs — Plain-text protected columns ↔ bytea.
WorkflowProtectedJsonConverter.cs   — JsonElement ↔ bytea.
WorkflowProtectionStampInterceptor.cs
                                   — SaveChanges interceptor that stamps `protection_version`
                                    on entities being saved when an IWorkflowDataProtector is
                                    registered.
WorkflowStorageServiceCollectionExtensions.cs
                                   — IWorkflowCoreBuilder.AddEfCoreStorage(connectionString) +
                                    DI registrations for the three store interfaces (composite
                                    + read + retention bound to the same scoped instance).
```

## Setup

```csharp
services.AddWorkflowCore(configuration)
        .AddEfCoreStorage(connectionString)
        // Optional PHI encryption — without it, protected columns hold plaintext UTF-8 bytes:
        .AddWorkflowDataProtector<AesGcmPhiProtector>();
```

`AddEfCoreStorage` registers:

- `WorkflowDbContext` (private to this plugin, schema = `workflow`).
- `EfCoreWorkflowStore` once as scoped, then re-bound to `IWorkflowStore`,
  `IWorkflowReadStore`, `IWorkflowRetentionStore` — same instance, three views.
- `IWorkflowStorageMigrator` (manual migration trigger).
- `WorkflowMigrationHostedService` by default (pass `autoMigrate: false` if you run migrations
  yourself via the migrator service).

## Schema

| Table | Purpose |
|---|---|
| `workflow.workflow_definitions` | Authored graphs. Unique on `(tenant_id, owner_kind, owner_id, trigger_kind)`. Optional `display_name`. |
| `workflow.workflow_runs` | One row per run. Carries `workflow_snapshot` (frozen graph), `static_context` + `steps_outputs` + `return_value` (JSON-typed protected columns), `parent_run_id` / `parent_step_id` for sub-workflow chains, `xmin` system column for concurrency. |
| `workflow.workflow_step_executions` | One row per step instance. Cascade-deleted with the run. `resolved_config` + `outputs` + `last_error` are protected columns. `is_long_running` drives lane filter. |
| `workflow.__EFMigrationsHistory` | Plugin-private history table. Lowercase column names (`migration_id`, `product_version`) per the snake_case convention. |

### Indexes

| Index | Columns | Purpose |
|---|---|---|
| `pk_*` | `id` | Primary keys, all entities. |
| `ix_workflow_definitions_tenant_id_owner_kind_owner_id_trigger_` | `(tenant_id, owner_kind, owner_id, trigger_kind)` UNIQUE | Locator hot path. |
| `ix_workflow_definitions_tenant_id_created_at` | `(tenant_id, created_at DESC)` | Definition list view. |
| `ix_workflow_runs_tenant_id_trigger_source_kind_trigger_source_` | `(tenant_id, trigger_source_kind, trigger_source_id)` | Trace-by-source lookups. |
| `ix_workflow_runs_tenant_id_created_at` | `(tenant_id, created_at DESC)` | Run list view. |
| `ix_workflow_runs_status_finished_at` | `(status, finished_at)` | Retention purge path. |
| `ix_workflow_runs_parent_run_id` | `(parent_run_id)` | Sub-workflow chain navigation. |
| `ix_workflow_runs_parent_step_id` | `(parent_step_id)` | Auto-resume parent step on child completion. |
| `ix_workflow_step_executions_pending_lane_next_attempt` | `(is_long_running, next_attempt_at) WHERE status='pending'` | **Partial index** — worker claim hot path. Collapses to ~1% of total rows at scale. |
| `ix_workflow_step_executions_waiting_lane_next_attempt` | `(is_long_running, next_attempt_at) WHERE status='waiting'` | **Partial index** — timeout sweeper. |
| `ix_workflow_step_executions_run_id_created_at` | `(run_id, created_at)` | GetStepsForRun queries with implicit chronological order. |
| `ix_workflow_step_executions_tenant_id` | `(tenant_id)` | Tenant-scoped purge / "delete all PHI for tenant X". |

### Foreign keys

| FK | On delete |
|---|---|
| `workflow_runs.definition_id` → `workflow_definitions.id` | RESTRICT — definitions can't be deleted while runs reference them. Callers that legitimately want to drop a definition (e.g. author removed a workflow from a form's settings) call `IWorkflowRetentionStore.PurgeRunsByDefinitionAsync` first; the explicit two-step makes the "run history goes with the definition" decision auditable rather than an invisible cascade. FK column has no app index — definition deletes are admin-rare; if it becomes a bottleneck, add a composite index that also serves a real query. |
| `workflow_runs.parent_run_id` → `workflow_runs.id` | **SET NULL** — when retention purges a parent run, descendant children are orphaned but stay alive. Critical for fire-and-forget sub-workflows: a parent that finishes early and gets purged otherwise drags a still-suspended child along under CASCADE, even though the child is doing legitimate work. RESTRICT can't be used because `ExecuteDeleteAsync`'s `ORDER BY` governs only LIMIT selection, not per-row delete order, so the planner could delete a parent before its children and trip the constraint. SET NULL atomically nulls the back-pointer when the parent row goes away — no ordering hazard, and `ParentRunId` is purely an audit / observability pointer plus the `MaxSubRunsPerRun` count source (only consulted while the parent is alive). Auto-resume of wait-mode parents goes through `ParentStepId`, not this FK. |
| `workflow_runs.parent_step_id` → `workflow_step_executions.id` | SET NULL — when a step is purged, child runs lose the back-pointer gracefully. |
| `workflow_step_executions.run_id` → `workflow_runs.id` | CASCADE — steps go with their run. |

## Migrations

Migrations live in `Migrations/` here, applied by the auto-registered hosted service or
manually via `IWorkflowStorageMigrator`. The plugin's history table sits in the same
`workflow` schema (`workflow.__EFMigrationsHistory`) so its evolution is independent from any
consumer DB context.

Generating a new migration:

```bash
dotnet ef migrations add MyMigration \
    --project Hipaa.Backend/Services/Plugins/Plugins.Workflow.Storage.EFCore \
    --startup-project Hipaa.Backend/Services/App/App.Web \
    --context WorkflowDbContext
```

## PHI encryption

Optional, opt-in via `IWorkflowDataProtector` registration (engine plugin's
`AddWorkflowDataProtector<T>()` builder method). Pivots without schema change:

- **Storage**: every protected column is `bytea`. Without protector, bytes are UTF-8 of the
  string / JSON form. With protector, bytes are `[0x80 magic byte] || ciphertext`. Mixed-mode
  safe: a row written before encryption was enabled stays readable (no magic byte → fall back
  to UTF-8 plaintext).
- **Magic byte 0x80**: chosen because it's a UTF-8 continuation byte — never the first byte
  of valid UTF-8 text. So a leading 0x80 unambiguously signals "encrypted blob" without
  reserving in-band magic strings.

Two converters share the envelope:

- `WorkflowProtectedStringConverter` — `string ↔ byte[]` for `abort_reason`, `last_error`.
- `WorkflowProtectedJsonConverter` — `JsonElement ↔ byte[]` for `static_context`,
  `steps_outputs`, `return_value`, `resolved_config`, `outputs`. Skips a per-evaluation
  deserialize and prevents consumer code from stuffing malformed strings into JSON-typed
  columns.

`protection_version` (varchar 64, nullable) on each protected entity carries the key id used
to write the row — stamped by `WorkflowProtectionStampInterceptor` at SaveChanges:

- `Added` entries always stamp.
- `Modified` entries stamp only when at least one protected (converter-mapped) property is
  itself modified — so a status-only update doesn't stamp the current version onto a row whose
  ciphertext was actually written under an older key.

Operators query `protection_version` to find rows still on a rotated-out key (see SQL recipes
below).

## Capacity diagnosis

Each query answers a concrete operational question — paste them into psql / a DB client and use
the interpretation note to decide whether a setting needs bumping. Not pretty enough for a
dashboard but enough to drive the first round of pool sizing.

### Pool sizing

**Backlog by lane (current pending queue):**

```sql
SELECT
    is_long_running,
    COUNT(*) FILTER (WHERE next_attempt_at <= now()) AS ready_now,
    COUNT(*) FILTER (WHERE next_attempt_at >  now()) AS waiting_for_retry,
    COUNT(*)                                          AS total_pending
FROM workflow.workflow_step_executions
WHERE status = 'pending'
GROUP BY is_long_running;
```

`ready_now` per lane consistently `> 0` while `WorkerCount × BatchSize` (or
`LongRunningWorkerCount × BatchSize`) workers are running means that pool is undersized. Bump
the corresponding worker count.

**Oldest pending step's wait time:**

```sql
SELECT
    is_long_running,
    MAX(EXTRACT(EPOCH FROM (now() - next_attempt_at)))::int AS oldest_ready_seconds
FROM workflow.workflow_step_executions
WHERE status = 'pending' AND next_attempt_at <= now()
GROUP BY is_long_running;
```

Healthy values are around `PollIntervalSeconds` (≤ 3-5s with default settings). Stable values
of `oldest_ready_seconds > 30s` confirm the lane is undersized.

**Throughput last hour, by lane:**

```sql
SELECT
    is_long_running,
    COUNT(*) FILTER (WHERE status = 'completed') AS completed,
    COUNT(*) FILTER (WHERE status = 'dead')      AS dead,
    COUNT(*)                                      AS total
FROM workflow.workflow_step_executions
WHERE completed_at > now() - INTERVAL '1 hour'
GROUP BY is_long_running;
```

Divide `completed` by 3600 for steps/sec served by each pool. If the figure is below the
arrival rate (= rate of new pending rows), the queue grows unbounded — add workers or raise
`BatchSize`.

### Mistagged actions

**Fast-lane actions whose p95 duration approaches the lane timeout:**

```sql
SELECT
    kind,
    COUNT(*)                                                                                       AS samples,
    percentile_cont(0.5)  WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (completed_at - created_at))) AS p50,
    percentile_cont(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (completed_at - created_at))) AS p95,
    percentile_cont(0.99) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (completed_at - created_at))) AS p99
FROM workflow.workflow_step_executions
WHERE status = 'completed'
  AND is_long_running = false
  AND completed_at > now() - INTERVAL '24 hours'
GROUP BY kind
HAVING percentile_cont(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (completed_at - created_at))) > 10
ORDER BY p95 DESC;
```

Any kind whose `p95 > FastLaneActionTimeoutSeconds / 2` is a candidate for `IsLongRunning => true`
on its `IActionType` — currently it's hogging fast-pool slots for half their budget per call.

### Worker health

**Currently running steps and how long they've been running:**

```sql
SELECT id, run_id, kind, is_long_running, attempt_count,
       EXTRACT(EPOCH FROM (now() - updated_at))::int AS running_seconds
FROM workflow.workflow_step_executions
WHERE status = 'running'
ORDER BY updated_at;
```

- Fast-lane rows with `running_seconds > FastLaneActionTimeoutSeconds` are about to be
  force-cancelled by the per-step CTS (normal protection, not a problem).
- Anything with `running_seconds > 5 × FastLaneActionTimeoutSeconds` indicates a worker died
  mid-action — the step won't recover until the next worker startup releases it (or until
  `Retention.EnableStalePurge` cleans the run after `StaleRunningRetentionDays`).

**Stuck running rows (dead workers, or in-flight at cancel time):**

```sql
SELECT s.id, s.run_id, s.kind, s.is_long_running,
       EXTRACT(EPOCH FROM (now() - s.updated_at))::int AS stuck_seconds,
       r.status AS run_status
FROM workflow.workflow_step_executions s
JOIN workflow.workflow_runs r ON r.id = s.run_id
WHERE s.status = 'running'
  AND s.updated_at < now() - INTERVAL '5 minutes'
ORDER BY s.updated_at;
```

Two normal sources of these rows:

1. **Worker process died without graceful drain.** `run_status = 'running'`. If this appears
   regularly, audit host shutdown — likely `HostOptions.ShutdownTimeout` is shorter than
   `ShutdownDrainSeconds + buffer` and the OS is force-killing.
2. **Cancel during in-flight action.** `run_status = 'failed'`. Cancel only writes the run
   row; the step keeps running until the action returns, then the worker writes its real
   outcome. If the worker subsequently died, the step is stuck. Stale-purge cleans these via
   `Retention.EnableStalePurge` after `StaleRunningRetentionDays`.

Filter by `run_status` to separate the two: rows with `run_status = 'failed'` are cancellation
fallout, not worker death.

### Retry pressure

**Average attempt count, recent completions:**

```sql
SELECT
    is_long_running,
    AVG(attempt_count)::numeric(4,2)            AS avg_attempts,
    COUNT(*) FILTER (WHERE attempt_count > 1)   AS retried,
    COUNT(*)                                     AS total
FROM workflow.workflow_step_executions
WHERE completed_at > now() - INTERVAL '1 hour'
GROUP BY is_long_running;
```

`retried / total > 5%` is suspicious. Two common causes:

1. Action genuinely flaky (HTTP source unstable) — fix at the action level (timeouts, retries
   inside the action) or at the wire (rate limiting, circuit breaker).
2. Worker shutdowns interrupting steps — drain budget transient errors retry on the next
   startup. If you see this aligned with deployment cadence, raise `ShutdownDrainSeconds` and
   the host's `ShutdownTimeout`.

`avg_attempts` for the fast lane > 1.1 with no other obvious issues = `FastLaneActionTimeoutSeconds`
is too tight for whatever workloads got tagged fast. Either bump the setting or tag the slow
actions long-running.

**Distribution of attempts on dead-lettered steps:**

```sql
SELECT kind, attempt_count, COUNT(*) AS n
FROM workflow.workflow_step_executions
WHERE status = 'dead'
  AND completed_at > now() - INTERVAL '24 hours'
GROUP BY kind, attempt_count
ORDER BY kind, attempt_count;
```

Shows where each action kind tends to give up. If a kind always dies on attempt 1 — it's
non-transient (validation error, missing config). If it tends to die at `MaxAttempts` after
ramping through retries — transient errors are real but exceed the configured budget; consider
bumping `MaxAttempts` or `BackoffSeconds`.

### Run-level inspection

**What's parked in waiting (suspended runs):**

```sql
SELECT r.id AS run_id, r.tenant_id, r.trigger_kind,
       s.kind AS waiting_step_kind,
       EXTRACT(EPOCH FROM (now() - s.updated_at))::int  AS waiting_seconds,
       CASE WHEN s.next_attempt_at = 'infinity' THEN NULL
            ELSE EXTRACT(EPOCH FROM (s.next_attempt_at - now()))::int
       END AS seconds_until_timeout
FROM workflow.workflow_runs r
JOIN workflow.workflow_step_executions s ON s.run_id = r.id
WHERE r.status = 'suspended' AND s.status = 'waiting'
ORDER BY s.updated_at
LIMIT 50;
```

`seconds_until_timeout IS NULL` = waiting forever (Approve without a deadline). Long-running
entries flag forgotten approvals, sub-workflows whose parent never resumed, etc. Doesn't
affect pool sizing — it's a business-process signal, useful for ops dashboards.

**Hot tenants (top 10 by recent volume):**

```sql
SELECT tenant_id,
       COUNT(*)                                      AS runs_last_hour,
       COUNT(*) FILTER (WHERE status='failed')       AS failed,
       SUM(EXTRACT(EPOCH FROM (COALESCE(finished_at, now()) - started_at)))::int AS total_run_seconds
FROM workflow.workflow_runs
WHERE created_at > now() - INTERVAL '1 hour'
GROUP BY tenant_id
ORDER BY runs_last_hour DESC
LIMIT 10;
```

If one tenant is responsible for the bulk of volume, the answer is rarely "raise the global
worker count" — it's app-level rate limiting or moving that tenant onto a dedicated pool /
process / cluster.

## Operational SQL

Read protected (currently plaintext) JSON column as text:

```sql
SELECT
    id,
    started_at,
    protection_version,
    length(static_context) AS bytes,
    -- Detection: first byte 0x80 (=128) means encrypted blob
    CASE
        WHEN length(static_context) > 0
         AND get_byte(static_context, 0) = 128
        THEN '<encrypted, version=' || COALESCE(protection_version, 'unknown') || '>'
        ELSE convert_from(static_context, 'UTF8')
    END AS static_context_text
FROM workflow.workflow_runs
ORDER BY started_at DESC
LIMIT 5;
```

Find rows that need re-encryption after rotating from `v1` to `v2`:

```sql
SELECT id, started_at, protection_version, status
FROM workflow.workflow_runs
WHERE protection_version = 'v1'         -- or IS NULL for legacy plaintext
  AND status IN ('running', 'suspended');

SELECT id, run_id, kind, protection_version
FROM workflow.workflow_step_executions
WHERE protection_version IS DISTINCT FROM 'v2';
```

Manual decrypt for AES-GCM-256 envelopes via plpython3u (see `IWorkflowDataProtector` impl in
the consumer for the wire format — typically `[1B version][2B keyId BE][12B nonce][ciphertext][16B tag]`):

```sql
CREATE EXTENSION IF NOT EXISTS plpython3u;

CREATE OR REPLACE FUNCTION workflow.decrypt_phi(
    payload bytea, key_hex text
) RETURNS text AS $$
    from cryptography.hazmat.primitives.ciphers.aead import AESGCM
    if not payload or payload[0] != 0x80:
        return payload.decode('utf-8') if payload else ''
    body    = bytes(payload[1:])
    version = body[0]
    key_id  = int.from_bytes(body[1:3], 'big')
    nonce   = body[3:15]
    ct      = body[15:]
    aesgcm  = AESGCM(bytes.fromhex(key_hex))
    return aesgcm.decrypt(nonce, ct, None).decode('utf-8')
$$ LANGUAGE plpython3u;

SELECT workflow.decrypt_phi(static_context, '<key-hex>') FROM workflow.workflow_runs WHERE id = '...';
```

## Why "Postgres-only"

The engine relies on vendor-specific primitives:

- `FOR UPDATE SKIP LOCKED` for the worker claim hot path — no portable equivalent.
- `xmin` system column for concurrency tokens — auto-bumped by the row visibility map.
- Partial indexes — collapse claim-path index size by ~99% at scale.
- `bytea` + `xid` types — different column types per backend.
- `ANY($1::uuid[])` parameter arrays — `WHERE id IN (...)` shape.
- `RAISE EXCEPTION` in DO blocks — guard for the encryption-migration Down path.

Pretending the consumer can pick a provider would be misleading; we take the connection
string and own the rest.

## What's NOT here

- The engine itself — see `Plugins.Workflow.Engine`.
- The contracts — see `Plugins.Workflow.Abstractions`.
- App-specific PHI protector implementation (the actual AES-GCM key handling) — that's in
  `App.Infrastructure`.
