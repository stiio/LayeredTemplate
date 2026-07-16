# Plugins.Workflow.Engine

The runtime — implements `IWorkflowDispatcher`, `IWorkflowRunner`, `IWorkflowResumer`,
`IWorkflowCanceller`, `IWorkflowRestarter`, the `IActionType` registry, the background
`WorkflowEngineWorker` (claims and dispatches steps with two-pool lane separation), the
`WorkflowRetentionWorker` (low-frequency purge sweeper), graph validator, and the three
built-in expression engines (Static / Liquid / JS) plus a curated set of generic action types
(Transform, Condition, Switch, FailRun, FinishRun, ForEach, Delay, RunWorkflow).

Plugin-style: configure once at startup, extend through DI registration. No DB access —
everything goes through `IWorkflowStore` (implemented by `Plugins.Workflow.Storage.EFCore` or
any custom store).

## Contents

- [Folder map](#folder-map)
- [Setup](#setup)
- [Settings (`WorkflowEngineSettings`)](#settings-workflowenginesettings)
- [Built-in actions](#built-in-actions)
- [Long-running lane](#long-running-lane)
- [Graceful shutdown](#graceful-shutdown)
- [Worker batch & failure handling](#worker-batch--failure-handling)
- [Cancel / Restart / Resume](#cancel--restart--resume)
- [Expression engines](#expression-engines)
- [Default globals (camelCase)](#default-globals-camelcase)
- [Extension hooks](#extension-hooks)
- [Telemetry](#telemetry)
- [Adding a new action type](#adding-a-new-action-type)
- [Adding an expression engine](#adding-an-expression-engine)
- [What's NOT here](#whats-not-here)

## Folder map

```
Actions/                          — Built-in action types: Transform, Condition, Switch,
                                    FailRun, FinishRun, ForEach, Delay, RunWorkflow. Drop a
                                    new file here + register it in AddWorkflowCore() to add
                                    another generic primitive.

Expressions/                      — Expression evaluation pipeline.
├── Engines/                       — Static, Liquid, JS engine impls + Liquid renderer + cache.
├── Extensions/                    — ILiquidFilter, ILiquidExtension, IJsFunction, IJsExtension
│                                    (extension contracts) + DefaultContext{Liquid,Js}Extension
│                                    (camelCase globals: tenantId, runId, etc.).
├── ExpressionResolver.cs          — Walks deserialized config, evaluates Expr<T> leaves.
│                                    Two-phase: non-transient at step build (persisted),
│                                    transient just-in-time in the worker (never persisted).
└── ExpressionModelBuilder.cs      — Internal helper assembling the model dictionary
                                    (static context + steps outputs) handed to engines for
                                    each evaluation.

Services/                          — Engine pipeline pieces.
├── ActionTypeRegistry.cs          — IActionType lookup by Kind.
├── StepExecutionBuilder.cs        — Resolves a node's config and emits a Pending step record;
│                                    stamps IsLongRunning from the action type so the worker
│                                    lane filter sees the right value.
├── WorkflowDispatcher.cs          — IWorkflowDispatcher — high-level entry: lookup definition,
│                                    enforce nesting / sub-run caps, hand to runner, flush.
├── WorkflowRunner.cs              — IWorkflowRunner — start a run from intent + definition.
├── WorkflowResumer.cs             — IWorkflowResumer — atomic Waiting → Completed transition
│                                    + fan-out + run-completion check.
├── WorkflowCanceller.cs           — IWorkflowCanceller — operator-driven termination.
├── WorkflowRestarter.cs           — IWorkflowRestarter — manual replay (snapshot or current-def).
├── WorkflowFanOut.cs              — Edge-walking + run-completion check + parent-step
│                                    auto-resume cascade. Shared between worker + resumer.
├── WorkflowValidator.cs           — Graph invariants (cycles, references, ports, single
│                                    start node, edge-port consistency).
├── WorkflowEngineWorker.cs        — BackgroundService loop: claim → dispatch → enqueue →
│                                    expire stale waiting steps → save (per step).
├── WorkflowRetentionWorker.cs     — BackgroundService that runs the IWorkflowRetentionStore
│                                    purge methods on a configurable schedule.
├── WorkflowActivitySource.cs      — Internal ActivitySource wrapper + WorkflowTags constants
│                                    (every span tag the engine emits).
└── HostStartupBarrier.cs          — Holds back background workers until ApplicationStarted —
                                    so they don't race the storage migration runner.

WorkflowEngineSettings.cs          — Polling cadence, batch size, retry backoff, step caps,
                                    worker pool counts, lane timeouts, shutdown drain,
                                    retention sub-settings.
WorkflowCoreServiceCollectionExtensions.cs
                                   — Composition root: AddWorkflowCore(...) + the
                                     IWorkflowCoreBuilder fluent surface
                                     (AddLiquidFilter<T>, AddLiquidExtension<T>,
                                     AddJsFunction<T>, AddJsExtension<T>,
                                     AddWorkflowDataProtector<T>).
```

## Setup

```csharp
services.AddWorkflowCore(configuration)
        .AddEfCoreStorage(connectionString)              // from the storage plugin
        .AddLiquidFilter<PhoneFormatFilter>()            // your own
        .AddJsFunction<GetPresignedUrlJsFunction>()
        .AddWorkflowDataProtector<AesGcmPhiProtector>(); // optional PHI encryption

// Custom action types — register as scoped:
services.AddScoped<IActionType, SendEmailActionType>();
services.AddScoped<IActionType, HttpRequestActionType>();
```

`AddWorkflowCore` registers:

- The eight built-in action types (Transform, Condition, Switch, FailRun, FinishRun, ForEach,
  Delay, RunWorkflow).
- The three expression engines + `IExpressionResolver`.
- `DefaultContextLiquidExtension` + `DefaultContextJsExtension` (expose `tenantId`, `runId`,
  `definitionId`, `actorUserId`, `triggerSourceKind`, `triggerSourceId`, `isDryRun` as globals
  in both engines — same camelCase names in both).
- `ActionTypeRegistry`, `WorkflowValidator`, `StepExecutionBuilder`.
- `WorkflowDispatcher`, `WorkflowRunner`, `WorkflowResumer`, `WorkflowCanceller`,
  `WorkflowRestarter`, `WorkflowFanOut`.
- `WorkflowEngineWorker` and `WorkflowRetentionWorker` as `HostedService`s.

## Settings (`WorkflowEngineSettings`)

Bound from configuration section `WorkflowEngineSettings` (override via the optional
`settingsSection` parameter to `AddWorkflowCore`). Knobs:

| Setting | Default | Purpose |
|---|---|---|
| `MaxAttempts` | 1 | Total attempts per step before dead-lettering. |
| `PollIntervalSeconds` | 30 | Fallback poll cadence of an idle worker loop. With the EF Core storage's LISTEN/NOTIFY push (on by default) workers wake in milliseconds and this only bounds lost-notification recovery + retry-backoff pickup; lower it to a few seconds when running a storage without a push primitive. |
| `MaintenanceIntervalSeconds` | 5 | Cadence of the single per-process maintenance loop (expired-waiting timeout sweep). Bounds suspend-deadline granularity — a Delay fires within this many seconds past its deadline. |
| `BookmarkSweepIntervalSeconds` | 1 h | Cadence of the signal-bookmark reconciliation sweep — pure hygiene (the Waiting-guard prevents wrong resumes; the signaler eagerly deletes what it consumes), hence rare. 0 disables. |
| `StuckStepRecoverySeconds` | 1 h | Crash backstop: `running` steps whose `updated_at` is older than this return to `pending` (attempt stays counted — the first soft failure after recovery dead-letters via MaxAttempts; pure hard-crash loops are terminated by `Retention.EnableStaleFail` at the run level). Must exceed the longest legitimate execution (lane budgets + drain); raise/disable if the long-lane timeout is disabled. 0 disables. |
| `BatchSize` | 10 | Steps claimed per polling iteration. |
| `BackoffSeconds` | `[30, 120, 600, 3600, 21600]` | Sequential backoff per retry attempt; last value repeats past the tail. Ignored when `MaxAttempts = 1`. |
| `MaxStepsPerRun` | 200 | Hard cap on total step records per run; trips `abort_reason="step_cap"`. |
| `MaxVisitsPerNode` | 50 | Per-node visit cap. Bodies of legitimate loops (ForEach) count once per iteration, so this must be ≥ `MaxLoopIterations`. |
| `MaxLoopIterations` | 25 | Hard cap on iterations of a single ForEach. |
| `MaxNestingLevel` | 3 | Depth cap on sub-workflow chains (top-level = 0). |
| `MaxSubRunsPerRun` | 3 | Direct sub-run quota per parent run. |
| `MaxNodesPerGraph` | 200 | Validator rejects graphs above this. |
| `MaxLiquidOutputChars` | 256 KB | Hard cap on a single Liquid render's output. |
| `MaxResolvedConfigChars` | 256 KB | Hard cap on a step's persisted resolved config; exceeding it fails the step at build time with advice to mark heavy fields transient or pass references instead of content. |
| `WorkerCount` | 1 | Concurrent fast-pool worker loops in-process (FOR UPDATE SKIP LOCKED on the storage side prevents double-claims). |
| `LongRunningWorkerCount` | 0 | Optional separate pool for `IsLongRunning=true` actions. When > 0, fast pool runs `FastOnly` lane, this pool runs `LongOnly` lane. Default 0 = single pool, no lane filter. |
| `ShutdownDrainSeconds` | 30 | Per-action grace after SIGTERM. The in-flight action gets this long to finish naturally before its CT fires; after shutdown signal, claimed-but-untouched steps are released back to `pending`. |
| `FastLaneActionTimeoutSeconds` | 30 | Hard per-action timeout for the fast lane. |
| `LongLaneActionTimeoutSeconds` | 300 | Optional hard per-action timeout for the long lane — a safety net against actions hung forever on external I/O. Counts the attempt like a transient failure. Set 0 to disable (shutdown drain stays the only deadline). |
| `Retention.EnableFinishedPurge` | `false` | Periodically purge `Completed`/`Failed` runs older than `FinishedRunRetentionDays`. |
| `Retention.EnableStaleFail` | `false` | Periodically mark `Running` runs idle longer than `StaleRunningRetentionDays` as `Failed` with `abort_reason = "stale: …"` (trace preserved); the finished purge deletes them later like any failed run. |
| `Retention.SweepIntervalSeconds` | 12 h | How often the retention worker sweeps. |
| `Retention.FinishedRunRetentionDays` | 30 | Threshold for finished-run purge. |
| `Retention.StaleRunningRetentionDays` | 7 | Idle threshold past which a `Running` run is marked `Failed` as stale. |
| `Retention.BatchSize` | 1000 | Per-iteration delete batch. |

## Built-in actions

| Kind | Purpose |
|---|---|
| `Transform` | Set named variables for downstream Liquid/JS via `steps.<key>.<name>`. |
| `Condition` | Boolean branch — fires `true` or `false` port. |
| `Switch` | Multi-way branch — `match-first` (if/elseif/else), up to 5 branches + `default`. |
| `FailRun` | Terminator: sets the run to `failed` with a reason; doesn't fire any port. |
| `FinishRun` | Terminator: sets the run to `completed` with an optional `return_value` (consumed by sub-workflow auto-resume). |
| `ForEach` | Loop body — iterates a resolved array, evaluating one step per item. |
| `Delay` | Suspends the step for a specified duration (`seconds` is an `Expr<int?>` — computable from run data); sweeper resumes it via the `done` port when the deadline elapses. |
| `RunWorkflow` | Spawns a sub-workflow run — `fire-and-forget` (returns `started` immediately) or `waitForCompletion` (suspends and resumes on the child's terminal state; optional `timeoutSeconds` expression fires `timedOut`). |
| `RunLiquid` | Renders a dynamically-supplied Liquid template (text from vars / prior steps, not authored in the graph) against the run context; `result` is the rendered string, or parsed JSON with `isJson: true`. Same engine, cache, filters, and limits as config expressions. |

App-specific actions (`SendEmail`, `HttpRequest`, custom domain ops) live on the consumer
side. `HttpRequestActionType` should override `IsLongRunning => true` so it runs on the
dedicated long-pool when one is configured.

## Long-running lane

`IActionType.IsLongRunning` (default `false`) marks an action whose synchronous body routinely
takes hundreds of ms or more (HTTP, slow S3, etc). Stamped onto each step row at creation time
in `is_long_running`. With `LongRunningWorkerCount > 0`:

- Fast pool (`WorkerCount`) claims only `is_long_running = false` rows.
- Long pool claims only `is_long_running = true` rows.
- A handful of slow HTTP steps can no longer starve fast Transform/Condition steps.

Default `LongRunningWorkerCount = 0` keeps the single-pool legacy behaviour where fast workers
claim everything.

## Graceful shutdown

When SIGTERM arrives, each fast / long worker:

1. Schedules the in-flight action's CT to cancel after `ShutdownDrainSeconds` (default 30s).
2. Lets the action finish naturally if it can; otherwise the action sees `OperationCanceledException`
   and the step is recorded as a transient error for retry.
3. After the current step, checks the stop signal — if set, releases any claimed-but-untouched
   steps back to `pending` (decrements `attempt_count` so the bump from claim is reversed) and
   exits the polling loop.

Pair with `services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(60))`
on the host so it actually waits for the engine to drain.

## Worker batch & failure handling

Idle wait: when a claim comes back empty the loop doesn't blindly sleep `PollIntervalSeconds` —
it waits on `IWorkflowWorkSignal`, a latching per-lane wake-up event. A push-capable storage
(the EF Core plugin's LISTEN/NOTIFY pair) pulses it the moment new steps are committed, so
dispatch-to-pickup latency is milliseconds while the poll interval only serves as the safety
net for lost notifications. Without a pulser the wait just times out — plain interval polling.

Per-step DI scope model: a polling iteration claims pending step IDS in a short-lived scope
(the claim SQL commits immediately), then loads, executes, and flushes every step in its OWN
scope — the same lifetime an action would see inside a web request. Consumer scoped services
an action takes by constructor can't leak state into the next step (which may belong to another
run / tenant), and each step gets a fresh identity map (an operator cancel committed mid-batch
is visible to the very next step). Per-step save commits results incrementally — if the host
kills us between step N and N+1, steps 1..N are durably persisted. The expired-waiting timeout
sweep and the bookmark reconciliation sweep run on a separate single per-process maintenance
loop (`MaintenanceIntervalSeconds`), each expired step handled in its own scope too.

If a step's processing throws (DB blip, anything else):

- The step's scope is disposed unsaved — nothing to clean up, no cross-step tracker pollution.
- The step is NOT marked as processed.
- Subsequent steps continue, each in a fresh scope.
- After the loop, claimed-but-unprocessed step ids are released back to `pending` via
  `ReleaseClaimedStepsAsync` (decrements `attempt_count`).

A fast-lane action timeout is the exception: releasing would refund the attempt while
`next_attempt_at` stays in the past (instant re-claim → hot loop), so the worker instead
applies the standard transient-error outcome in a fresh scope — backoff retry, dead-letter at
`MaxAttempts`. Shutdown-drain cancellation keeps the release path (a deploy must not consume
attempts).

There is no optimistic-concurrency guard between worker writes and racing cancel/restart.
Cancel writes only to `workflow_runs.status` and leaves step rows alone, so the only race
window is the worker's terminal `running → completed` overwriting cancel's `running → failed`.
The window is single-digit milliseconds (between worker's fresh `GetRunAsync` and its
`SaveChanges`); cancel is an admin op; we accept this as a non-issue.

## Cancel / Restart / Resume

| Service | Use |
|---|---|
| `IWorkflowResumer.ResumeAsync(WorkflowResumeCommand, ct)` | Finalises a `Waiting` step on a chosen port + `JsonElement?` payload. Guard, action wake-up hook, fan-out, and flush commit as ONE storage transaction; the atomic guarded UPDATE makes duplicate calls 409-style no-ops, and any post-guard failure rolls back to `Waiting`. |
| `IWorkflowCanceller.CancelAsync(WorkflowCancelCommand, ct)` | Operator termination. Atomic SQL flips active steps to `dead` + run to `failed`. Sub-workflow parents resume on `failed` port. |
| `IWorkflowRestarter.RestartAsync(WorkflowRestartCommand, ct)` | Manual replay. Mode `UseSnapshot` replays against the frozen graph; `UseCurrentDefinition` re-fetches the live definition. Old run is never mutated. |

## Expression engines

| Engine | Use |
|---|---|
| `static` | Literal value — string is the value as-is, JSON literals for non-string targets. |
| `liquid` | Fluid template — `{{ vars.answers.email }}`, `{% if %}`, custom filters. |
| `js` | Sandboxed Jint — full JS expressions/bodies, async/await supported (host functions can return `Task<T>`). |

All three see the same model:

- `vars.<key>` — every key the trigger source put in `WorkflowStartIntent.Variables`.
- `trigger.kind` / `trigger.isDryRun` / `trigger.sourceKind` / `trigger.sourceId` — engine
  metadata.
- `steps.<node-key>.<output>` — outputs from upstream completed steps.

The two namespaces (`vars` / `trigger`) never overlap, so consumer keys can never collide
with engine-supplied data.

## Default globals (camelCase)

`DefaultContextLiquidExtension` and `DefaultContextJsExtension` register the same set in both
engines, identical names so an author can move a snippet between Liquid and JS without
renaming. Available out of the box:

- `tenantId`
- `runId`
- `definitionId`
- `actorUserId` (nullable)
- `triggerSourceKind` (nullable)
- `triggerSourceId` (nullable)
- `isDryRun`

## Extension hooks

| Interface | Scope | Purpose |
|---|---|---|
| `ILiquidFilter` | Scoped | One named Liquid filter (`{{ x \| my_filter }}`). Constructor injection — can take DbContext, etc. |
| `ILiquidExtension` | Scoped | Per-render hook — Fluid `TemplateOptions` + `TemplateContext` access. Use for value converters, MemberAccessStrategy.Register<T>(), globals beyond filters. |
| `IJsFunction` | Scoped | One named JS function (`getPresignedUrl(id)`). Returns a `Delegate` per evaluation so it can close over tenant context. Async via `Task<T>`. |
| `IJsExtension` | Scoped | Per-evaluation hook — fresh `Jint.Engine` access. Use for advanced setup beyond named functions. |
| `IWorkflowDataProtector` | Singleton | Optional symmetric encryption for PHI columns. Without it, protected columns store plaintext UTF-8 bytes; with it, ciphertext with a `[0x80 magic][bytes]` envelope. See storage plugin README for the storage contract. |

All four expression hooks receive the workflow `ExpressionEvaluationContext` (tenant id,
run id, actor, trigger source, dry-run flag) so tenant scoping is straightforward.

## Telemetry

`WorkflowActivitySource.Instance` (source name `Hipaa.Workflow`) emits spans at every notable
boundary: `workflow.run.dispatch`, `workflow.run.resume`, `workflow.run.cancel`,
`workflow.run.restart`, `workflow.step.execute`, `workflow.step.timeout`,
`workflow.action.execute`, `workflow.fanout.enqueue_next`, `workflow.fanout.check_completion`,
`workflow.retention.sweep`. Tags are constants in `WorkflowTags` (run / step / tenant /
definition ids, kind, lane, output port, attempt count, outcome, …). Wire OpenTelemetry once
on the host:

```csharp
services.AddOpenTelemetry().WithTracing(t => t.AddSource(WorkflowTelemetry.ActivitySourceName));
```

`StartActivity` returns null when no listener is registered — zero overhead in setups that
don't ship traces.

## Adding a new action type

1. Drop a class implementing `ActionType<TConfig>` in your project (or `Actions/` here for
   built-ins).
2. Define the config POCO with `Expr<T>` properties for dynamic fields.
3. Declare static `OutputPorts` (Normal / Error / Always).
4. Implement `ExecuteAsync(ActionContext<TConfig>, CancellationToken)`.
5. Override `IsLongRunning => true` if the body is genuinely slow.
6. If the action suspends (returns `OnSuspend`), override `OnStepResumedAsync` to choose the
   wake-up port and `OnStepTimedOutAsync` for graceful timeout handling (e.g. a `timedOut` port).
   Both default to a loud non-transient `OnError` — a suspending action MUST override them.
7. Register: `services.AddScoped<IActionType, YourAction>();`.

The frontend's `ListActionTypes` API surfaces it automatically — no FE changes needed beyond
config-editor UI for the new shape.

## Adding an expression engine

Implement `IExpressionEngine` with a unique `Name` (e.g. `"yaml"`). Register
`services.AddScoped<IExpressionEngine, YourEngine>()`. The resolver picks it up by `Name`.

## What's NOT here

- DB access — that's the storage plugin (`Plugins.Workflow.Storage.EFCore`).
- App-specific actions or triggers — that's `App.Infrastructure`.
- Frontend — `Hipaa.Frontend`.
