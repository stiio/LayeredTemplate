# HIPAAtizer Workflow vs Elsa Core v2 — сравнительный анализ

Дата: 2026-04-30. Бейслайн нашего движка — текущий код (`Plugins.Workflow.{Abstractions,Engine,Storage.EFCore}` + App-side actions). Elsa — `2.12` (последняя стабильная серия v2; v3 в репо есть, но **в анализ не включена**).

Цель — выяснить, **где** и **насколько** наш самописный движок выигрывает / проигрывает зрелому industry-стандарту, чтобы понять направление дальнейшего развития: добивать своё решение vs мигрировать.

## TL;DR

| Ось | Наш движок | Elsa v2 | Победитель |
|---|---|---|---|
| Объём кода (core, без миграций/моделей) | ~15.7K LOC, 125 .cs | ~73K LOC v2, 1 945 .cs | Наш — компактнее в 5× |
| Встроенные actions / activities | 14 (9 engine + 5 app) | ~100 | **Elsa** — на порядок шире |
| Storage backends | 1 (EF Core / Postgres) | 3 (EF Core, MongoDB, YesSql) | **Elsa** |
| Concurrency control | `FOR UPDATE SKIP LOCKED` (Postgres) | `SemaphoreSlim` per-store + distributed lock (Medallion) | **Наш** — построено на индексе, не блокировке |
| Storage shape | Колоночный — `static_context`, `steps_outputs`, `outputs`, `resolved_config` отдельные protected JSON | Один JSON-блоб `Data` на весь `WorkflowInstance` | **Наш** — searchable, partial-decrypt |
| Многотенантность | `TenantId` обязателен везде, индексы tenant-first | `ITenantAccessor` + `TenantId` опционально, дефолт = null, multi-tenant Temporal не закрыт (TODO в коде) | **Наш** — встроено в фундамент |
| PHI / encryption | `IWorkflowDataProtector` + bytea + `[1B 0x80 magic][cipher]` envelope, key id встроен per-value в blob для key rotation | Только `IDataProtection` для http-callback токенов; field-level не нашёл | **Наш** |
| Multi-port outcomes / параллелизм | Single-port-per-step + Switch / Condition / ForEach | Multi-port + явные Fork/Join + ParallelForEach + Compensation | **Elsa** — больше моделей |
| Suspend/Resume | `Waiting` step + `IWorkflowResumer.ResumeAsync(stepId, port, payload)` атомарно | `Bookmark`-таблица + `BookmarkProvider` per-activity, hash-индекс для матчинга, `BookmarkIndexer` пере-создаёт всё каждый шаг | **Elsa** — гибче (bookmark-провайдеры расширяемы), наш — дешевле (одна таблица, один UPDATE) |
| Triggers | Не отдельная сущность — `WorkflowTriggerKinds` строки + `IWorkflowDispatcher.DispatchAsync` | `Trigger` сущность = bookmark до старта; `TriggerIndexer` + hash-matching | **Elsa** — для event-driven multi-source сценариев это важно |
| Expression engines | Static / Liquid (Fluid) / JS (Jint) | Liquid (Fluid) / JS (Jint) / Sql / Literal / Json / Switch / Variable. **C# / Roslyn нет в v2.** | Размен (мы покрываем 90% кейсов; Elsa имеет Sql/Json/Switch как отдельные движки) |
| Restart / Replay | `IWorkflowRestarter` — UseSnapshot / UseCurrentDefinition; новый run, старый не мутируется | `WorkflowReviver` для retry; полноценного replay execution log нет | **Наш** — UX явнее |
| Worker | In-process polling-pool с FOR UPDATE SKIP LOCKED, 2 lane (fast / long-running), per-step SaveChanges, graceful drain | `QueuingWorkflowDispatcher` поверх Rebus (или Hangfire / Orleans), один instance на consumer | Для нашего масштаба — наш проще; для cross-process scale-out — **Elsa** |
| Versioning | Definition один на (tenant + ownerKind + ownerId + triggerKind), upsert, runs хранят `WorkflowSnapshot` (frozen graph) — не зависят от live definition | `Version` колонка, `IsLatest`/`IsPublished`, runs привязаны к конкретной `DefinitionVersionId` | Размен (наш = single-active-version + snapshot; Elsa = full history) |
| Designer (visual editor) | Свой ReactFlow + antd редактор, ~3K LOC TS | Stencil-based studio, `@elsa-workflows/elsa-workflows-studio@2.11.0`, AntV X6 + dagre + monaco | **Elsa** богаче, но наш под HIPAAtizer-форму заточен |
| Tests | 5 файлов, 1.7K LOC, 46 unit/functional tests | 138 файлов, ~4.6K LOC tests | **Elsa** — больше |
| Зрелость (зависимости) | .NET 10, EF Core 10, актуальные NuGet | .NET Standard 2.1 + .NET 6, MediatR 12, Hangfire 1.7, Jint 3.0-beta, AutoMapper 12 | **Наш** — современнее |
| Время на интеграцию в новый проект | Только наша же кодовая база, нулевая | Bundle (`Elsa.Server.Web`) ставится за час, но настройка под PHI / multi-tenant — недели | Размен |
| Память на run | ~1 step row в трекере + run record + graph cache (per-batch); ~1-5 KB | Весь `WorkflowInstance` в памяти всегда + DI-scope per run + transient activities на каждый шаг | **Наш** — компактнее, особенно на длинных runs |
| Roundtrips на шаг | Claim (1 SQL), GetRun (1 SELECT в Local cache hit), UpdateStep (Local), SaveChanges (1 BATCHED UPDATE+INSERT) — итого 2-3 roundtrips | Find instance (1 SELECT), Save instance (FindAsync + SaveChanges = 2), Bookmarks DELETE+INSERT (2-N) — итого 4-6 roundtrips | **Наш** |

## 1. Архитектура исполнения

### Наш движок

**Модель**: linear pipeline с явными ports. Действия (`IActionType`) возвращают `ActionExecutionResult { OutputPort, Outputs, IsSuspended, TerminatesRun, Error, IsTransient, ReturnValue, SuspendTimeoutSeconds }`. Engine fan-out'ит ровно одну исходящую edge на единственный fired port — никаких параллельных fan-out из коробки.

Параллелизм автор моделирует явно: `Fork` отсутствует, его роль играют `Switch` (роутинг по условиям) и `RunWorkflow` (sub-workflow в fire-and-forget — тогда родитель идёт дальше, а дочерние крутятся независимо).

`ForEach` — итеративный (single-port-per-step), state-aware: action читает свой own previous outputs из `run.steps_outputs[node_key]` и инкрементирует index. Frozen items на первой итерации.

**State machine**:
- Run: `running` → `suspended` (пока хоть один Waiting шаг) → `running` (после resume) → `completed` / `failed`.
- Step: `pending` → `running` → `completed` / `failed` / `dead` / `waiting`.

Двигает state — `WorkflowEngineWorker` (`ProcessBatchAsync`): claim batch с `FOR UPDATE SKIP LOCKED`, по каждому step вызывает `IActionType.ExecuteAsync`, применяет `ActionExecutionResult` через `ApplyResultAsync` (выставляет step.Status, fan-out next, run-completion check). Single-port engine означает что run.completion check — простой: «нет ни одного активного step → run terminal».

**Suspend/Resume**: step.Status = `waiting`, `next_attempt_at` = deadline. `IWorkflowResumer.ResumeAsync(runId, stepId, port, payload, ct)` — атомарный `UPDATE WHERE status='waiting'` (защита от двойного resume), затем fan-out по выбранному порту.

### Elsa v2

**Модель**: Activity-based с named outcomes (произвольные строки). Edges — `(source.Activity, source.Outcome) → target.Activity`. Multi-port: одна activity может иметь любое число outcomes; `Outcomes(IEnumerable<string>)` помогает выпустить сразу несколько.

Параллелизм: явные `Fork` (выпускает все ветки одной activity), `Join` (ждёт `WaitAll`/`WaitAny` от inbound transitions, чистит scopes), `ParallelForEach`. `Compensable`/`Compensate`/`Confirm` — встроенный механизм компенсаций.

**State machine** (`WorkflowExecutionContext`): `Idle` → `Running` → `Suspended` / `Faulted` / `Cancelled` / `Finished`. `Faults` — стек, не одно поле; allows nested fault handling.

**Runner**: `WorkflowRunner.RunCoreAsync` — синхронный цикл `while (HasScheduledActivities)` в одной задаче. После burst — публикация `WorkflowExecutionBurstCompleted`, `BookmarkIndexer.IndexBookmarksAsync` пере-создаёт ВСЕ bookmarks (DELETE all + INSERT each) даже если они не изменились. Это даёт чистый снепшот, но N+1 в худшем случае.

**Suspend/Resume через Bookmarks**: каждая `BlockingActivity` производит `Bookmark { Hash, Model (JSON), ModelType, ActivityType, ActivityId, WorkflowInstanceId, CorrelationId, TenantId }` через зарегистрированный `IBookmarkProvider<TBookmark, TActivity>`. Внешнее событие → `BookmarkFinder.FindBookmarksAsync(activityType, IEnumerable<IBookmark>)` строит `WHERE activity_type=X AND tenant_id=Y AND hash IN (...)`. Один индексированный запрос матчит сразу все возможные.

### Сравнение по функциям

| Кейс | Наш | Elsa v2 |
|---|---|---|
| Линейный pipeline (10 шагов) | + (естественно) | + |
| Branching (if-else, switch) | Switch / Condition | If / Switch (богаче — поддерживает SwitchExpression handlers) |
| Loop | ForEach (single, sequential) | ForEach + ParallelForEach + While + For |
| Параллельные ветки | Только через RunWorkflow fire-and-forget | Fork (реальный multi-thread bursts), ParallelForEach |
| Join из параллельных | — (нужно делать через RunWorkflow wait-mode) | Join (WaitAll/WaitAny, EagerJoin) |
| Compensation (rollback) | — | Compensable / Compensate / Confirm activities |
| Sub-workflow с возвратом | RunWorkflow + FinishRun → returnValue парент'у | RunWorkflow activity + signals |
| Human approval / wait | Approve action, suspend + resume API | UserTask activity, signal-based resume |
| Timer / Delay | Delay action | Timer / Cron / StartAt + Hangfire или Quartz |

**Вывод**: Elsa предлагает богаче control-flow (явный Fork/Join, ParallelForEach, Compensation). Наш движок намеренно single-port — это архитектурный выбор, упрощающий рассуждения о ходе выполнения и delegating параллелизм на RunWorkflow + child runs. Для типового HIPAAtizer-сценария (форма → нотификация → PDF → опционально approve) single-port достаточен; для оркестраций с явным fan-out / compensation Elsa переигрывает.

## 2. Storage / persistence

### Наш — column-oriented + protected

Три EF Core таблицы:
- `workflow_definitions` — `(tenant_id, owner_kind, owner_id, trigger_kind)` natural key, `graph` jsonb с raw нодами и edges.
- `workflow_runs` — `id, tenant_id, definition_id, name, trigger_kind, trigger_source_*, started_at, finished_at, status, abort_reason, workflow_snapshot (frozen graph string), static_context (JsonElement), steps_outputs (JsonElement), return_value (JsonElement?), nesting_level, parent_run_id, parent_step_id, created_at, updated_at`.
- `workflow_step_executions` — `id, run_id, tenant_id, node_id, kind, name, predecessor_execution_id, trigger_port, resolved_config (JsonElement), is_long_running, status, output_port, attempt_count, next_attempt_at, completed_at, last_error, outputs (JsonElement?), created_at, updated_at`.

**Protected JSON / string columns** — не jsonb, а `bytea` через `WorkflowProtectedJsonConverter` / `WorkflowProtectedStringConverter`:
- Без `IWorkflowDataProtector` — UTF-8 plaintext.
- С регистрацией — `[0x80 magic byte] || ciphertext`. Mixed-mode read поддержан (старые plaintext rows читаются параллельно с зашифрованными).
- Key id встроен per-value в blob (wire format), поэтому re-encryption sweep инспектирует значения, а не per-row stamp.
- Hot path остаётся на `JsonElement` (не string round-trip): `WorkflowProtectedJsonConverter` хранит UTF-8 bytes от `el.GetRawText()`, парсинг только на чтение.

Concurrency: `FOR UPDATE SKIP LOCKED` в `ClaimPendingStepIdsAsync` (raw SQL под Postgres). Run-record concurrency token раньше был (`xmin`), потом удалён — не нужен: worker держит запись в Local трекере per-step scope'а между Get → Update. Race window между cancel и worker terminal write — accepted (single-digit ms).

### Elsa v2 — JSON-blob

Один `Data` shadow-property string на `WorkflowInstance`:
```csharp
// EntityFrameworkWorkflowInstanceStore.OnSaving:
entity.Property("Data").CurrentValue = serializer.Serialize(new {
  entity.Variables, entity.Input, entity.Output,
  entity.ActivityData, entity.BlockingActivities,
  entity.ScheduledActivities, entity.Faults, entity.Scopes,
  entity.Metadata, entity.CurrentActivity
});
```

Что это означает на практике:
- **Поиск по содержимому невозможен** — только по индексированным колонкам (status, tenant_id, correlation_id, etc.).
- **Каждое сохранение — полный re-serialize** всего instance state. На длинных workflow с большим `ActivityData` — растущий cost.
- **Concurrency** — `SemaphoreSlim _semaphore = new(1)` на инстанс store. Защищает только в рамках одного процесса; multi-process требует distributed lock провайдер (Medallion.Threading).
- Bookmarks отдельно хранятся в нормальной таблице с индексом `(ActivityType, TenantId, Hash)`. Каждый burst — DELETE all bookmarks for instance + INSERT all current.

### Сравнение

| Аспект | Наш | Elsa v2 |
|---|---|---|
| Шейп | Колоночный + opaque JSON для PHI-payload | Один JSON-блоб на instance |
| Searchability | По tenant / status / trigger_source / definition_id / created_at напрямую SQL'ом | Только через индексированные колонки instance |
| Selective decrypt | Можно расшифровать только нужное поле | Всё-или-ничего: расшифровка ради чтения = full deserialize |
| PHI policy | Встроена via interceptor + key versioning | Не встроена |
| Indexing для list views | `(tenant_id, created_at DESC)` композитный, `(tenant_id, trigger_source_kind, trigger_source_id)` для drill-down | 12 индексов на instance, но без trigger_source pattern |
| Concurrency | FOR UPDATE SKIP LOCKED — индекс-driven | SemaphoreSlim + Medallion distributed lock |
| Migration story | Стандартная EF migration history (`__EFMigrationsHistory_workflow`) | EF / Mongo / YesSql каждый со своими миграциями |
| Per-step row | Да, отдельная таблица | Нет, всё внутри instance JSON |

**Вывод**: подход сильно разный. Наш ориентирован на **observability + compliance** (per-step row + protected columns + tenant-first indexes). Elsa — на **простоту хранения**: один blob на instance, легко мигрировать между провайдерами, но цена — невозможность аналитического запроса без вытаскивания и parse'а instance в памяти.

Для дашборда «покажи мне все runs за месяц у workspace X где fired step Y» — у нас один SQL запрос; у Elsa нужно либо догружать инстансы и фильтровать в памяти, либо вытаскивать данные в отдельную аналитическую базу.

Для multi-process scale-out Elsa полагается на distributed lock как отдельный механизм; наш использует SKIP LOCKED — это фундаментально дешевле (нет roundtrip к лок-провайдеру и нет contention на одном ключе).

## 3. Worker и распределённое исполнение

### Наш

In-process pool через `WorkflowEngineWorker` (`BackgroundService`). Настройки:
- `WorkerCount` (fast pool) + `LongRunningWorkerCount` (long pool) — два независимых полла.
- При `LongRunningWorkerCount > 0` fast pool берёт строки с `is_long_running=false` (FastOnly lane), long — с `is_long_running=true` (LongOnly lane). FOR UPDATE SKIP LOCKED + `WHERE is_long_running` — гарантия что один worker не будет блокировать другой класс работ.
- Per-step `SaveChangesAsync` (раньше было per-batch), `ReleaseClaimedStepsAsync` возвращает claimed-but-unprocessed обратно в pending при graceful shutdown. `ShutdownDrainSeconds` (default 30) даёт actions время доработать после SIGTERM.

Per-step CTS: `FastLaneActionTimeoutSeconds` (default 30) — hard upfront budget на fast lane, чтобы стучая HTTP без таймаута не камп нужны worker thread. Long lane без upfront cap.

Sub-workflow: `RunWorkflow` action → `IWorkflowDispatcher.DispatchAsync` создаёт child run в той же БД-транзакции worker'а. WaitForCompletion — родитель Suspended до завершения ребёнка, FanOut.CheckRunCompletionAsync auto-resume по `parent_step_id`.

Distributed scale-out: несколько процессов с тем же воркером работают на одной БД, FOR UPDATE SKIP LOCKED разводит их безопасно. **Очередей нет — БД сама очередь.**

### Elsa v2

`QueuingWorkflowDispatcher` поверх Rebus — отправляет `ExecuteWorkflowInstanceRequest` в очередь, консьюмеры на других процессах подхватывают. Транспорты: AzureServiceBus, RabbitMQ, MassTransit, Kafka, MQTT, in-memory. `ICommandSender` → bus, `WorkflowChannel` → queue name.

Альтернативы:
- `HangfireWorkflowDispatcher` — каждый dispatch создаёт Hangfire-job; persistance через Hangfire storage.
- `Orleans` — `WorkflowInstanceGrain` single-activation grain даёт сериализацию по ID без внешнего лока (умное решение для high-throughput).

Background tasks (Timer, Cron, StartAt) — отдельный модуль `Elsa.Activities.Temporal.{Hangfire,Quartz}`. `StartJobs.cs` при старте берёт distributed lock, читает `IBookmarkFinder` / `ITriggerFinder` для всех timer-bookmarks, материализует их как Hangfire/Quartz jobs.

### Сравнение

| Параметр | Наш | Elsa v2 |
|---|---|---|
| Транспорт | БД (FOR UPDATE SKIP LOCKED) | Rebus bus / Hangfire / Orleans |
| Setup стоимость | 0 | Поднять очередь (кролик / SB / Hangfire schema) |
| Scale-out | N процессов на одну БД | N consumers на одну очередь |
| Latency | poll interval (default 3s) | Bus push — мс |
| Backpressure | Через `BatchSize` + poll | Bus встроен (prefetch, in-flight cap) |
| Failure isolation | Crashed worker → claim → лежит в running до stale-purge | Bus auto-redelivery + dead-letter queues |
| Multi-tenant fairness | Не встроено (FIFO по created_at) | Можно через partitioned queues |
| Per-step granularity | Да, каждый step — отдельный claim | Нет, burst пройдёт целиком в одной задаче (если не Suspend) |
| Timer activities | Delay через тот же mechanism (Waiting + sweeper) | Отдельный Hangfire/Quartz job per timer |

**Вывод**: для one-process / few-process deployment'а наш простее и быстрее (нет network roundtrip к bus). Для high-throughput cross-region setup Elsa с Orleans/Rebus гораздо мощнее.

Наш poll-interval 3s — наблюдаемая latency на старт нового step'а в среднем 1.5s. У Elsa с Rebus — миллисекунды. Если важна low-latency — это серьёзный аргумент в сторону Elsa или замены нашего полла на LISTEN/NOTIFY (Postgres has it!).

Per-step granularity — наша отличительная черта. У Elsa один burst = один Save. Если burst — это 50 activities и 49я падает, повторный запуск пройдёт от начала burst. У нас — повтор только на упавшем step'е, остальные уже committed.

## 4. Capabilities (actions / activities)

### Наш — 14 типов

**Engine plugin** (9):
- Control: Condition, Switch, ForEach
- Lifecycle: FailRun, FinishRun, Delay, RunWorkflow, SetRunName
- Composition: Transform (set variables)

**App plugin** (5, HIPAAtizer-specific):
- SendEmail, HttpRequest, Approve, GenerateDocxPdf, SetSubmissionIdentifier

Регистрация: `services.AddScoped<IActionType, T>()` — scoped на batch lifetime. **Re-resolved новый instance не на каждый шаг, а на каждый scope.** Это компромисс между transient (Elsa) и singleton.

### Elsa v2 — ~100 типов

Основные категории (см. отчёт по Elsa):
- Control flow: If, Switch, For, ForEach, ParallelForEach, While, Break, Finish, Fork, Join (10).
- Primitives: SetVariable, SetTransientVariable, SetName, SetContextId, Fault, Inline (6).
- Compensation: Compensable, Compensate, Confirm (3).
- Workflows: RunWorkflow, Correlate (2).
- HTTP: HttpEndpoint (trigger!), SendHttpRequest, WriteHttpResponse, Redirect (4).
- Email: SendEmail (MailKit).
- File: 7.
- BlobStorage: 4.
- Sql: ExecuteSqlCommand, ExecuteSqlQuery.
- Messaging integrations: 6 (AzureSB, RabbitMq, Kafka, Mqtt, MassTransit, Rebus).
- Telnyx (телефония): 17.
- Temporal: Timer, Cron, StartAt, ClearTimer.
- UserTask, Conductor, Entity, Rpa.Web, Dropbox, Startup.

Регистрация: **Transient** на каждый запуск. `ActivatorUtilities.GetServiceOrCreateInstance` каждый вызов.

### Сравнение

Очевидно: Elsa **на порядок шире**. Но это включает интеграции которые в HIPAAtizer-проекте либо не нужны (Telnyx, Dropbox, Rpa.Web), либо есть отдельной билдинг-блок (Email = ASP.NET MailKit прямо в App.Infrastructure).

Реальный gap для нашего проекта:
- **Sql / ExecuteCommand** — нет, но в HIPAAtizer raw SQL в workflow выглядит сомнительно.
- **BlobStorage** — нет напрямую, но `GenerateDocxPdf` пишет в S3 через Stowage.
- **MessageReceived** триггеры — нет (у нас trigger = `WorkflowTriggerKinds` строка + `IWorkflowDispatcher.DispatchAsync` вручную из доменного кода). Если завтра нужны webhook-triggers / cron-triggers / message-bus-triggers — придётся писать с нуля.
- **Compensation** — нет. Если в будущем нужны workflow с rollback semantics — отсутствует.
- **Real Fork / Join / ParallelForEach** — отсутствует. Single-port остаётся главным архитектурным компромиссом.

## 5. Triggers

### Наш

Trigger в нашем мире — это не сущность, а **строка-discriminator**: `WorkflowTriggerKinds.SubmissionCompleted`, `SubmissionUpdated`, etc. + custom (`SubWorkflow`).

Старт run'а — через явный вызов `IWorkflowDispatcher.DispatchAsync` из доменного кода (например, `SubmissionWorkflowTrigger.OnSubmissionCompleted`). Engine не сканирует «какие workflow подходят к этому событию» — это работа доменного слоя.

Плюс: простая модель, нет magic-matching.
Минус: для каждого нового источника событий — писать свой триггер вручную. Для HTTP webhook trigger / message bus trigger / cron trigger нужен явный bridge.

### Elsa v2

Trigger = activity с атрибутом `[Trigger]` + `IBookmarkProvider`. При публикации definition `TriggerIndexer` создаёт `Trigger` rows. Внешнее событие → `IWorkflowLaunchpad.FindWorkflowsAsync(WorkflowsQuery(activityType, IBookmark))` ищет matching triggers через hash.

Из коробки:
- `HttpEndpoint` — слушает HTTP path/method.
- `Timer` / `Cron` / `StartAt` — планировщик через Hangfire/Quartz.
- `*MessageReceived` для каждого транспорта.
- `EventReceived`, `EntityChanged` (Entity Framework hooks!).
- Telnyx call-events.

То есть в Elsa **«какой workflow стартовать на это событие» — это работа движка, не доменного слоя**.

### Сравнение

| Кейс | Наш | Elsa v2 |
|---|---|---|
| Trigger — одно фиксированное событие в коде | + (явный Dispatch) | + (HTTP endpoint et al.) |
| Trigger — динамический «по содержимому события» (например, "когда appointment канселится в любом месте") | Нужно писать matching самому | Bookmark hash matching из коробки |
| Webhook-trigger | Писать руками | HttpEndpoint + публикация |
| Cron-trigger | Писать руками | Cron activity, рассылается через Hangfire |
| Multiple workflows на одно событие | Да, dispatcher не делает unique match — все matching definitions запускаются | Да, FindWorkflowsAsync возвращает массив |

Для HIPAAtizer-сценария «форма → workflow on submit» наш подход проще; для «когда лаб-результат приходит, проверить что это, и решить кому какой workflow стартовать» Elsa переигрывает.

## 6. Versioning

### Наш

Definition — один на (tenant + ownerKind + ownerId + triggerKind). При сохранении новых настроек — `UpsertDefinitionAsync` обновляет тот же row. Старая версия не сохраняется.

Run хранит `WorkflowSnapshot` (frozen graph string) внутри своей строки. Это значит: даже после `UpsertDefinitionAsync` старые runs продолжают работать на снапшоте — они не зависят от live definition.

Restart: `IWorkflowRestarter.RestartAsync` с двумя режимами:
- `UseSnapshot` — клонирует static_context, прогоняет снапшот старого run'а через новый run.
- `UseCurrentDefinition` — re-fetch live definition по id, использует её graph.

### Elsa v2

Каждый Save новой версии = инкремент `Version`, новая запись с тем же `DefinitionId`. `IsLatest` / `IsPublished` флаги. `WorkflowInstance` привязан к конкретному `DefinitionVersionId` (не `DefinitionId`) — апдейт definition не влияет на запущенные.

`WorkflowPublisher`:
- `New()` — Version=1, IsLatest=true.
- `PublishAsync` — снимает старые IsLatest/IsPublished, инкрементит, устанавливает свежие.
- `RetractAsync` — снимает IsPublished без инкремента.
- `GetDraftAsync` — клонирует latest published, делает draft.
- `DeleteAsync(VersionOptions.AllVersions)` — каскадно удаляет инстансы.

### Сравнение

| Аспект | Наш | Elsa v2 |
|---|---|---|
| Множественные версии в БД | Нет — только текущая | Да |
| Старые runs защищены от изменений definition | Да (через snapshot) | Да (через DefinitionVersionId) |
| Возможность audit «какая graph была на момент Run #X?» | Да (через snapshot column) | Да (через JOIN на старую версию definition) |
| Размер БД на одну definition | Малый (один row) | Растёт линейно с числом версий |
| Workflow восстановления старой версии | Нет (но можно скопировать snapshot из старого run'а) | Через `RevertAsync` |

**Вывод**: подход разный. Наш проще (один row, snapshot защищает прошлые runs), но не даёт UI «вернуться к предыдущей версии definition». Elsa поддерживает full version history с поддержкой revert.

Для HIPAAtizer на текущем этапе один-row + snapshot достаточен.

## 7. Designer

### Наш

ReactFlow + antd. Состоит из `WorkflowEditor.tsx`, `NodeConfigEditor.tsx`, `NodeInspector.tsx`, `NodeConfigModal.tsx`, canvas с `WorkflowActionNode`. Auto-layout через dagre. Per-action inspector форма (свой `<XxxFields>` компонент в `NodeConfigEditor.tsx`).

Конфигурация actions — `ExpressionEditor` с переключателем static/liquid/js. `ActionTypeMetadataDto` приходит с `/action-types` endpoint, описывает доступные типы и их output ports.

### Elsa v2

Stencil + TypeScript: `@elsa-workflows/elsa-workflows-studio@2.11.0`, AntV X6 (граф) + dagre (layout) + monaco-editor (через ajv). Богаче из коробки:
- Designer работает standalone и в дашборде.
- Поддержка version-history UI.
- TypeScript-definitions auto-generated (`TypeScriptDefinitionService`) — autocomplete в JS expressions.
- Свои модули `auth0`, `credential-manager`, `elsa-webhooks`, `elsa-workflows-settings`.

### Сравнение

Elsa designer старше и шире. Наш заточен под наш domain и легче кастомизируется. Если когда-нибудь понадобится поддержка multi-version / version-rollback / autocomplete для expressions — это отдельная работа.

## 8. Observability

### Наш

OpenTelemetry — `WorkflowActivitySource` с `ActivitySourceName = "Hipaa.Workflow"`. Spans: `workflow.run.dispatch`, `workflow.step.execute`, `workflow.action.execute`, `workflow.fanout.enqueue_next`, `workflow.fanout.check_completion`, `workflow.step.timeout`, `workflow.retention.sweep`. Tags `WorkflowTags.RunId`, `StepId`, `TenantId`, `Kind`, `StepLane`, `ActionResultType`, etc.

Serilog logging с structured scopes (`StepId`, `RunId`, `TenantId`, `Kind`, `AttemptCount`, `Lane`, `NodeKey`, `IsDryRun`, `NestingLevel`).

Per-step trace в БД: `workflow_step_executions` хранит `attempt_count`, `next_attempt_at`, `completed_at`, `last_error`, `outputs`, `resolved_config`. Frontend `WorkflowRunExecutionLog` отображает timeline.

### Elsa v2

`WorkflowExecutionLogRecord` — отдельная таблица (entity + EF config). Каждый шаг записывается. MediatR-нотификации `ActivityActivating`, `ActivityExecuting`, `ActivityResuming` etc. — расширяемая точка для собственного аудита.

OpenTelemetry — не нашёл встроенной интеграции в v2 (в v3 появилось).

Дашборд показывает execution log per-instance.

### Сравнение

Похоже. У нас встроен OTel, у Elsa — MediatR-нотификации (надо самому писать listener). У Elsa — отдельная execution log таблица с record-per-event; у нас — per-step row плюс в принципе достаточный (нет multi-event-per-step). Если когда-нибудь понадобится точный timeline (e.g. «когда именно произошло X внутри activity») — у нас этого нет.

## 9. Производительность

Без бенчмарков обе стороны сравниваем по архитектуре. Я бы делал две оценки.

### Throughput steps/sec на одной БД (Postgres)

**Наш**: bottleneck — `ClaimPendingStepIdsAsync` (raw SQL `FOR UPDATE SKIP LOCKED ... LIMIT N`). На batch_size=10, poll 3s, 4 worker'а — в idle ~3.3 batches/sec → 33 steps/sec. Под нагрузкой когда batch'и всегда полны — ограничено лочистикой Postgres + JSON сериализация. Estimate ~200-500 steps/sec на одной БД до того как FOR UPDATE станет hot path. Per-step roundtrips: 2-3 (claim + save), каждый — обычный pgx.

**Elsa с Rebus + EF**: bookmark indexer пере-создаёт ВСЕ bookmarks каждый burst — это N+1 INSERT/DELETE. Плюс полный JSON-блоб re-serialize на каждый Save. На simple workflow (3-5 шагов) — ~50-150 instances/sec на одном consumer; больший workflow — медленнее линейно. Distributed lock на каждый dispatch добавляет миллисекунды. Estimate worse than ours для simple workflows; better при правильной горизонтальной разнаправленности (Orleans-режим лучше всех).

### Memory на длинном run

**Наш**: per-run в памяти — только текущий step record + run record (загруженный для этого batch'а). FanOut кэширует graph per-batch. После batch'а scope dispose — все объекты под GC. Длинный run (50 шагов) — те же 5-10 KB working set в любой момент.

**Elsa**: весь `WorkflowInstance` всегда в памяти на время burst — включая `Variables`, `ActivityData`, `Scopes`, `BlockingActivities`, `ScheduledActivities`, `Faults`. Чем длиннее run / больше variables — тем больше memory. Plus Activities transient = аллокация на каждый шаг.

### Latency на step start

**Наш**: poll interval (default 3s). Step может ждать до 3s после dispatch. Можно понизить poll до 0.5s или заменить на LISTEN/NOTIFY — это работа.

**Elsa с Rebus**: bus push → consumer wake up → milliseconds. С Hangfire — секунды (job poll). С Orleans — миллисекунды.

### Сравнение

| Кейс | Наш | Elsa v2 |
|---|---|---|
| Simple workflow (5 шагов) throughput | Выше | Ниже (full instance re-serialize) |
| Long workflow (50+ шагов) memory | Лучше | Растёт линейно с шагами |
| Step start latency | 1-3s (poll) | Миллисекунды (bus) |
| Multi-process scale | Линейный (FOR UPDATE) до contention | Линейный с queue partitions |
| Cold start overhead | Минимальный (DI scope per batch) | Минимальный (DI scope per run) |

**Вывод**: на нашем профиле нагрузки (low-medium throughput, long-tail latency tolerance, single Postgres) — наш быстрее и легче. На enterprise scale (10K+ workflows/sec) Elsa с Orleans переигрывает.

## 10. Стабильность и зрелость

### Elsa

- Версия `2.12`, развитие v2 остановлено в пользу v3. Это значит: bug-fixes — да, новые features — нет.
- 138 тестовых файлов, ~4.6K LOC tests (на 73K LOC исходников = ~6% test ratio).
- Нет benchmark'ов в репо.
- Зависимости — `netstandard2.1` / `net6.0`, MediatR 12, AutoMapper 12, Hangfire 1.7, Jint 3.0-beta, Microsoft.Orleans 3.5. **Старые версии**, особенно Jint в beta.
- Production-проверена: используется в реальных проектах несколько лет, GitHub stars 7K+.
- TODO в коде: точечно, например multi-tenant Temporal не закрыт.

### Наш

- В активной разработке (.NET 10, EF Core 10, актуальные NuGet).
- 5 тест-файлов, 1.7K LOC tests, 46 unit/functional tests.
- Нет benchmark'ов.
- Нет production track record — движок дев-only.
- Архитектурные решения принимались осознанно (документированы в README + ADR в `Plan/`).

**Вывод**: Elsa проверена временем, наш — молод. Но Elsa v2 — frozen feature-set, и зависимости устаревают. Если выбирать stable platform — Elsa v3 (но это другая история, другой API).

## 11. Когда что использовать

### Наш движок выигрывает когда

- **HIPAA / PHI critical**: встроенный protected columns + key versioning + selective decrypt.
- **Multi-tenant by design**: TenantId везде, индексы tenant-first, нет TODO в core.
- **Postgres-first deployment**: используем родные фичи (FOR UPDATE SKIP LOCKED, jsonb, partial indexes).
- **Observability нужна на per-step granularity**: per-step row в БД + structured logging.
- **Simple-to-medium workflows**: 3-15 шагов с ясной линейной логикой, без compensation / parallel join.
- **Low operational overhead**: только Postgres + .NET процесс, никакого Rebus / Hangfire / Orleans.

### Elsa v2 выигрывает когда

- **Богатые встроенные интеграции**: HTTP webhooks / message bus triggers / cron / Telnyx / file ops без написания glue code.
- **Параллельные workflows с join**: Fork / Join / ParallelForEach / Compensation встроены.
- **Multi-source triggers**: bookmark hash matching находит подходящие workflows автоматически.
- **High-throughput cross-region**: Rebus / Orleans deployment.
- **Visual designer как product feature**: studio из коробки богаче.
- **Workflow versioning UX**: full version history + revert.

### Когда мигрировать?

- Если завтра нужны Fork/Join + Compensation + cron-triggers + multi-bus integrations — это **месяцы** дописывания нашего движка vs **недели** интеграции Elsa.
- Если HIPAA / PHI / multi-tenant критичны — наш движок ближе к этому из коробки; Elsa v2 потребует обёрток + custom serializers + audit-redactor + tenant filter.
- Если scale > 1K workflows/sec — Elsa с Orleans уже работает; наш потребует переход на LISTEN/NOTIFY + горизонтальной шардинг по tenant.

## 12. Гипотетический roadmap расширения

Если нужно подобрать gap'ы в нашем движке относительно Elsa без миграции:

| Фича | Сложность | Ценность для HIPAAtizer |
|---|---|---|
| LISTEN/NOTIFY вместо poll | Средняя | Низкая (3s latency приемлема) |
| Реальный Fork (запуск нескольких children в parallel под общим parent) | Средняя | Низкая (RunWorkflow fire-and-forget покрывает) |
| Join activity (wait-all / wait-any от children) | Высокая (single-port engine не помогает) | Низкая |
| Compensation activities | Высокая | Низкая (формы редко нуждаются в rollback'е) |
| HTTP webhook trigger (extend WorkflowTriggerKinds + endpoint) | Низкая | Средняя |
| Cron / scheduled trigger (background scheduler + sweeper) | Средняя | Средняя (для regulatory checks) |
| Message bus trigger (extend dispatcher) | Средняя | Низкая |
| Bookmark-hash matching (вместо прямого Dispatch) | Высокая | Низкая |
| Workflow versioning + revert UI | Средняя | Средняя |
| Sql action | Низкая | Низкая (раз сомнительно) |

Большинство — **средняя/низкая ценность** для нашего профиля; имеет смысл расширять только под конкретные запросы продукта, не превентивно.

## 13. Итоги

Наш движок — **точечный, well-scoped, production-grade под HIPAAtizer-сценарий**. Сильные стороны:
- PHI/encryption из коробки
- Многотенантность как фундамент
- Per-step observability
- Postgres-native concurrency
- Современный .NET 10 stack

Слабые относительно Elsa:
- Нет встроенного fan-out / join / compensation
- Меньше встроенных triggers / activities
- Workflow versioning минимальный
- Designer проще

Elsa v2 — **широкая платформа** с большим catalog'ом activities и multi-deployment topology. Сильные стороны:
- ~100 встроенных activities
- Реальный параллелизм
- Bookmark/Trigger симметричная модель
- Orleans / Rebus / Hangfire choice
- Visual studio богаче

Слабые относительно нашего:
- JSON-блоб persistence — нет searchability
- SemaphoreSlim concurrency — process-local
- PHI / multi-tenant — не fundament
- Активная разработка остановлена в пользу v3
- Зависимости устарели

**Рекомендация**: оставаться на самописке, расширять по точечным запросам продукта. Если в будущем потребуется complex workflow orchestration с параллелизмом / compensation — оценивать **Elsa v3**, не v2 (актуальный stack, активная разработка), но это уже другой research.
