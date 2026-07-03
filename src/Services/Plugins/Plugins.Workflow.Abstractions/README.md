# Plugins.Workflow.Abstractions

Public surface of the workflow engine. Defines the **contracts** that the engine implementation,
storage backends, action types, expression engines, and consumer apps share — without depending
on any of them.

This plugin has **zero NuGet references** beyond the .NET BCL (`System.Text.Json` is in-box).
A custom storage backend, third-party action type, or consumer trigger pulls only this assembly.

## Contents

- [Folder map](#folder-map)
- [Why this layering](#why-this-layering)
- [Key concepts](#key-concepts)
  - [`Expr<T>` — dynamic config values](#exprt--dynamic-config-values)
  - [`WorkflowStartIntent` + `IWorkflowDispatcher`](#workflowstartintent--iworkflowdispatcher)
  - [Run / step state machines](#run--step-state-machines)
  - [`IWorkflowStore` and its narrower views](#iworkflowstore-and-its-narrower-views)
  - [Cancel / restart / resume](#cancel--restart--resume)
  - [PHI encryption](#phi-encryption)
  - [`ActionContext` and `ExpressionEvaluationContext`](#actioncontext-and-expressionevaluationcontext)
  - [Pagination](#pagination)
- [Status enums (string constants)](#status-enums-string-constants)
- [Telemetry](#telemetry)
- [Adding a trigger kind](#adding-a-trigger-kind)
- [Versioning](#versioning)

## Folder map

```
Actions/        — IActionType (+ ActionType<TConfig> base; Execute + OnStepResumed /
                  OnStepTimedOut lifecycle hooks for suspending actions),
                  IActionTypeRegistry, ActionContext (run-time, JsonElement-typed),
                  ActionExecutionResult (port / suspend / terminate / error),
                  ActionPort (descriptor + Normal/Error/Always kind enum).
Expressions/    — Expr<T> wrapper + JSON converter, IExpressionEngine, IExpressionResolver,
                  ExpressionEvaluationContext (per-evaluation tenant context),
                  ExpressionEngines (name constants), ExpressionResolutionException.
Graph/          — WorkflowGraph + WorkflowNode + WorkflowEdge + WorkflowEdgeEndpoint + Position.
                  Wire format authors save (jsonb in workflow_definitions.graph).
Models/         — Run-time records swapped between engine and storage:
                  WorkflowDefinition, WorkflowStartIntent, WorkflowRunRecord, WorkflowStepRecord,
                  WorkflowRunStatus / StepExecutionStatus / WorkflowStepLane (constants + enum),
                  WorkflowRunStepStateSummary, WorkflowTriggerKinds,
                  WorkflowPagination, WorkflowPagedResult<T>, WorkflowRunFilter,
                  WorkflowDefinitionFilter.
Services/       — Engine entry-points and persistence boundary:
                  IWorkflowDispatcher (start a run end-to-end),
                  IWorkflowRunner (low-level start primitive),
                  IWorkflowResumer (resume Waiting steps),
                  IWorkflowCanceller (terminate a run),
                  IWorkflowRestarter (replay a finished run, snapshot or current-definition),
                  IWorkflowValidator (graph invariants),
                  IStepExecutionBuilder (internal hook shared between runner + worker),
                  IWorkflowReadStore / IWorkflowRetentionStore / IWorkflowStore
                    (read / purge / full persistence boundary),
                  IWorkflowWorkSignal (latching wake-up between "steps committed" and idle
                    worker loops — pulsed by push-capable storage, e.g. LISTEN/NOTIFY),
                  WorkflowConcurrencyException (optimistic-concurrency signal),
                  IWorkflowDataProtector (PHI encryption hook).
Telemetry/      — WorkflowTelemetry (ActivitySource name constant).
```

Each folder is a sub-namespace (`Hipaa.Backend.Plugins.Workflow.Abstractions.Actions`, etc.).

## Why this layering

- **Engine plugin** (`Plugins.Workflow.Engine`) implements the runner / dispatcher / resumer /
  canceller / restarter / validator / expression engines / built-in actions / hosted workers.
  Depends on this plugin + Fluid + Jint.
- **Storage plugin** (`Plugins.Workflow.Storage.EFCore`) implements `IWorkflowStore` (and
  transitively the read / retention sub-interfaces) for EF Core / Postgres. Depends on this
  plugin + EF Core. A future Mongo / SQL Server plugin slots in by implementing the same
  `IWorkflowStore` contract.
- **Consumer app** (`App.Infrastructure`) implements custom `IActionType`s
  (`SendEmailActionType`, `HttpRequestActionType`, …), optional `ILiquidFilter` /
  `ILiquidExtension` / `IJsFunction` / `IJsExtension`, a trigger facade
  (`SubmissionWorkflowTrigger` that builds a `WorkflowStartIntent` from a domain event), and
  optionally `IWorkflowDataProtector` for PHI encryption at rest.

## Key concepts

### `Expr<T>` — dynamic config values

Action config POCOs hold `Expr<T>` properties wherever the value should be a template:

```csharp
public class SendEmailConfig
{
    public Expr<string> To { get; set; } = new();
    public Expr<string> Subject { get; set; } = new();
    public Expr<string> BodyHtml { get; set; } = new();
}
```

Wire format on disk: `{ "engine": "static" | "liquid" | "js", "value": "<template>" }`. At
dispatch time the engine's `IExpressionResolver` walks the deserialized config and populates
each `Expr<T>.Resolved`. Actions read via the implicit `Expr<T> → T` conversion.

### `WorkflowStartIntent` + `IWorkflowDispatcher`

Universal entry point for any trigger source — the dispatcher looks up the matching definition,
spins up a run, and flushes the storage plugin's own DbContext:

```csharp
var result = await dispatcher.DispatchAsync(new WorkflowDispatchRequest
{
    TenantId = workspaceId,
    OwnerKind = "Form",
    OwnerId = form.Id,
    TriggerKind = WorkflowTriggerKinds.SubmissionCompleted,
    TriggerSourceKind = "Submission",
    TriggerSourceId = submission.Id,
    Variables = JsonSerializer.SerializeToElement(new { answers, meta, submission, form, workspace }),
    ActorUserId = currentUserId,
}, ct);
```

`Variables` is a `JsonElement?` (object); the engine stores it under `static_context.vars`.
Templates address keys as `{{ vars.answers.email }}` / `vars.answers.email` (Liquid / JS).
Engine-supplied metadata lives under a separate `trigger` namespace
(`{{ trigger.kind }}`, `{{ trigger.isDryRun }}`, …) — the two namespaces never overlap, so no
key in `Variables` can collide with engine-supplied data.

### Run / step state machines

`WorkflowRunStatus`: `running` | `suspended` | `completed` | `failed`.
`StepExecutionStatus`: `pending` | `running` | `waiting` | `completed` | `failed` | `dead`.

- A run is `suspended` when its only active step is `waiting` (Approve / Delay / RunWorkflow
  with `waitForCompletion`). The dedicated status keeps suspended runs out of the
  stale-running purge sweep.
- Constants (string) instead of enums so storage layers (jsonb, snake_case columns) can store
  them as plain strings without `HasConversion<string>()` boilerplate.

### `IWorkflowStore` and its narrower views

The persistence boundary is split into three nested interfaces so consumers don't depend on
methods they don't use:

- **`IWorkflowReadStore`** — `Get*`, `Find*`, `List*`, `Count*`. App-side handlers that just
  project workflow data into DTOs depend on this.
- **`IWorkflowRetentionStore`** — `PurgeFinishedRunsAsync`, `PurgeAllForTenantAsync`,
  `FailStaleRunningRunsAsync`. The retention background worker depends only on this.
- **`IWorkflowStore : IWorkflowReadStore, IWorkflowRetentionStore`** — composite, adds writes
  + worker hot path (`Claim*`, `Release*`, `AbortActiveSteps*`, `TryResumeWaitingStep*`,
  `GetStepStateSummary*`) + `SaveChangesAsync`. Engine internals depend on this.

Same EF Core implementation backs all three — DI registers it once and re-binds the narrower
interfaces to the same scoped instance.

### Cancel / restart / resume

- `IWorkflowCanceller.CancelAsync(WorkflowCancelCommand, ct)` — operator termination. Atomic
  `UPDATE` flips every active step to `dead` and the run to `failed`; if the run is a
  sub-workflow, fans the `failed` port up to the parent.
- `IWorkflowRestarter.RestartAsync(WorkflowRestartCommand, ct)` — manual replay. Two modes:
  `UseSnapshot` (frozen `workflow_snapshot` from the old run) or `UseCurrentDefinition`
  (re-fetch live definition by id). Original run is never mutated.
- `IWorkflowResumer.ResumeAsync(WorkflowResumeCommand, ct)` — finalises a `Waiting` step on a
  caller-supplied port + `JsonElement?` payload. Atomic `WHERE status='waiting'` guard makes
  duplicate resume calls 409-style no-ops.

Cancel deliberately does NOT touch step rows. In-flight actions run to completion and write
their real outcome (the trace shows what actually executed). The next step that would have
started sees `run.Status = Failed` in the worker's pre-action check and short-circuits to
`step.Status = Dead`. There is a microsecond race window where the worker's terminal write
(`running → completed`) can land just after cancel's (`running → failed`), with last-write-wins
on the run row — accepted as a non-issue: cancel is an admin op and the window is single-digit
milliseconds.

### PHI encryption

Optional. Register `IWorkflowDataProtector` and the EF Core backend will transparently encrypt
the JSON-typed protected columns (`static_context`, `steps_outputs`, `return_value`,
`resolved_config`, `outputs`) plus plain-text protected columns (`abort_reason`, `last_error`)
using a `[1B 0x80 magic][ciphertext]` envelope. Without registration, bytes are stored as
plaintext UTF-8. The key id used to seal each value is embedded in the ciphertext blob's wire
format, so a re-encryption sweep inspects individual values rather than a per-row stamp.

### `ActionContext` and `ExpressionEvaluationContext`

Threaded into action execution and expression evaluation. Both carry `TenantId`, `RunId`,
`StepExecutionId`, `ActorUserId`, `TriggerSourceKind`/`Id`, `IsDryRun` — custom actions and
custom Liquid filters / JS functions get tenant scoping for free
(e.g. `getPresignedUrl(fileId)` only returning URLs for files in `Evaluation.TenantId`).

### Pagination

`WorkflowPagination(Page, Limit)` + `WorkflowPagedResult<T>(Items, Page, Limit, TotalCount)` —
engine-internal pagination contract used by `IWorkflowReadStore.ListRunsAsync` and
`ListDefinitionsAsync`. App-side handlers translate from their own `PaginationRequest` shape.
Validation is `Validate()` → `ArgumentOutOfRangeException` on bad inputs; storage backends
call it at the top of every paged query.

## Status enums (string constants)

- `StepExecutionStatus`: `pending` | `running` | `completed` | `failed` | `dead` | `waiting`.
- `WorkflowRunStatus`: `running` | `suspended` | `completed` | `failed`.
- `ExpressionEngines`: `static` | `liquid` | `js`.

## Telemetry

`Telemetry/WorkflowTelemetry.ActivitySourceName` (`"Hipaa.Workflow"`) — register an
OpenTelemetry listener for this source to capture engine spans (run dispatch, step execute,
action invoke, resume, cancel, restart, fan-out, completion check, retention sweep). Tag
schema is in the engine plugin's `WorkflowTags` (see Engine README).

## Adding a trigger kind

Extend `WorkflowTriggerKinds` (constants class). The engine treats it as opaque — the only
contract is that `WorkflowDefinition.TriggerKind == WorkflowStartIntent.TriggerKind` for
dispatch to find the matching definition.

## Versioning

Public types here are the **stability contract** between engine and consumers. Breaking
changes — `WorkflowStartIntent` shape, `IActionType`, `IWorkflowStore` additions — require
coordinated updates across all three plugins + the consumer app. Bump the major version when
they happen.
