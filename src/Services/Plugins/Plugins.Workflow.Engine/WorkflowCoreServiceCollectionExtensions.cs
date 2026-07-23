using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Actions;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions.Engines;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions.Extensions;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayeredTemplate.Plugins.Workflow.Engine;

/// <summary>
/// Composition root for the workflow engine plugin. Registers all engine-internal services
/// (resolver, action-type registry, validator, runner, worker, expression engines) and exposes
/// a fluent <see cref="IWorkflowCoreBuilder"/> so storage / triggers / action types / Liquid+JS
/// extensions can chain on:
/// <code>
/// services.AddWorkflowCore(configuration)
///         .AddEfCoreStorage(connectionString)
///         .AddActionType&lt;SendEmailActionType&gt;()
///         .AddLiquidFilter&lt;PhoneFormatFilter&gt;()
///         .AddLiquidExtension&lt;MyLiquidExtension&gt;()
///         .AddJsFunction&lt;GetPresignedUrlJsFunction&gt;()
///         .AddJsExtension&lt;MyJsExtension&gt;();
/// </code>
/// </summary>
public static class WorkflowCoreServiceCollectionExtensions
{
    /// <summary>
    /// Wires the engine. Call once at startup. <see cref="IWorkflowStore"/> is NOT registered
    /// here — it's the storage plugin's job (e.g. <c>AddEfCoreStorage&lt;TDbContext&gt;()</c>).
    /// </summary>
    public static IWorkflowCoreBuilder AddWorkflowCore(
        this IServiceCollection services,
        IConfiguration configuration,
        string settingsSection = nameof(WorkflowEngineSettings))
    {
        services.Configure<WorkflowEngineSettings>(configuration.GetSection(settingsSection));

        // Compiled-template cache stays singleton; LiquidExpressionEngine itself is scoped
        // (registered below) because it composes per-call extensions that may take other scoped
        // services like DbContext.
        services.AddSingleton<ILiquidTemplateCache, LiquidTemplateCache>();

        services.AddScoped<IExpressionEngine, StaticExpressionEngine>();
        services.AddScoped<IExpressionEngine, LiquidExpressionEngine>();
        services.AddScoped<IExpressionEngine, JsExpressionEngine>();
        services.AddScoped<IExpressionResolver, ExpressionResolver>();

        // Default context extensions — expose tenantId / runId / etc. (camelCase, identical in
        // both engines) as Liquid+JS globals out of the box so authors don't wire them up in
        // every project.
        services.AddScoped<ILiquidExtension, DefaultContextLiquidExtension>();
        services.AddScoped<IJsExtension, DefaultContextJsExtension>();

        services.AddScoped<IActionTypeRegistry, ActionTypeRegistry>();
        services.AddScoped<IWorkflowValidator, WorkflowValidator>();
        services.AddScoped<IStepExecutionBuilder, StepExecutionBuilder>();
        services.AddScoped<IWorkflowRunner, WorkflowRunner>();
        // High-level entry: app handlers call dispatcher.DispatchAsync(...) instead of touching
        // runner / store directly. Dispatcher owns the SaveChanges of the plugin's DbContext.
        services.AddScoped<IWorkflowDispatcher, WorkflowDispatcher>();
        // Shared between worker (regular execution) and external resume callers (suspended-step
        // API). Encapsulates edge / Join / safety-cap rules.
        services.AddScoped<IWorkflowFanOut, WorkflowFanOut>();
        // Engine-internal per-scope units the worker resolves from each step's own DI scope:
        // step dispatch + result state machine, and the maintenance-loop work items (timeout
        // sweep / revert, stuck-running recovery, bookmark reconciliation). Registered by
        // concrete type — they're plumbing between the worker and scoped services, not a
        // consumer-facing contract.
        services.AddScoped<WorkflowStepExecutor>();
        services.AddScoped<WorkflowMaintenanceSweeper>();
        services.AddScoped<IWorkflowResumer, WorkflowResumer>();
        // Generic signal-wait fan-out: resolves bookmarks for an opaque (tenant, key) pair and
        // resumes every waiting run via the resumer. App-side facade (mirrors IWorkflowDispatcher);
        // domain actions register bookmarks on suspend, an App service signals on the matching event.
        services.AddScoped<IWorkflowSignaler, WorkflowSignaler>();
        // Operator-driven termination: run → Failed, all active steps → Dead, sub-workflow
        // parent (if any) gets resumed on its `failed` port.
        services.AddScoped<IWorkflowCanceller, WorkflowCanceller>();
        // Manual replay: clones static_context onto a brand-new run, either against the frozen
        // workflow_snapshot or the current workflow_definitions row.
        services.AddScoped<IWorkflowRestarter, WorkflowRestarter>();

        // Built-in action types — domain-agnostic primitives every consumer needs. App-specific
        // actions (SendEmail, HttpRequest, …) stay on the consumer side. There's no Join action
        // because the engine is single-port-per-step — runs are linear pipelines, not DAGs with
        // parallel arrivals to merge.
        services.AddScoped<IActionType, TransformActionType>();
        services.AddScoped<IActionType, ConditionActionType>();
        services.AddScoped<IActionType, SwitchActionType>();
        services.AddScoped<IActionType, FailRunActionType>();
        services.AddScoped<IActionType, FinishRunActionType>();
        services.AddScoped<IActionType, ForEachActionType>();
        services.AddScoped<IActionType, DelayActionType>();
        services.AddScoped<IActionType, RunWorkflowActionType>();
        // Generic suspend/signal pair over the bookmark primitive (ADR-025) — the domain-agnostic
        // layer beneath App adapters like WaitForm. WaitSignal parks on N opaque keys (wait-for-any);
        // SendSignal emits a signal that fan-out-resumes every waiter on the matching key.
        services.AddScoped<IActionType, WaitSignalActionType>();
        services.AddScoped<IActionType, SendSignalActionType>();
        // Engine-built-in: writes a Liquid/JS-computed label onto run.Name. Operator-facing
        // QoL — distinguish runs in the dashboard without inspecting their static_context.
        services.AddScoped<IActionType, SetRunNameActionType>();
        // Renders a dynamically-supplied Liquid template (text from vars / prior steps, not
        // authored in the graph) against the run context — same engine, cache, filters, limits
        // as config expressions.
        services.AddScoped<IActionType, RunLiquidActionType>();

        // Wake-up latch between "new steps committed" and idle worker loops. Deliberately a
        // singleton: worker loops wait on it, a push-capable storage plugin (EF Core's
        // LISTEN/NOTIFY listener) pulses it — both sides must resolve the same instance.
        // TryAdd so a consumer / test can substitute its own signal before AddWorkflowCore.
        services.TryAddSingleton<IWorkflowWorkSignal, WorkflowWorkSignal>();

        services.AddHostedService<WorkflowEngineWorker>();
        // Always-registered retention worker; effectively dormant unless
        // WorkflowEngineSettings.Retention.Enable* flags are turned on. Cheap to leave
        // registered — host startup cost is one BackgroundService.StartAsync that returns
        // immediately after kicking off ExecuteAsync, which itself short-circuits to return
        // when both flags are false.
        services.AddHostedService<WorkflowRetentionWorker>();

        return new WorkflowCoreBuilder(services);
    }

    // ===== Action types =====

    /// <summary>
    /// Register a consumer action type as a scoped service. <see cref="IActionType.Kind"/> is
    /// the id workflow nodes reference; the scoped <c>ActionTypeRegistry</c> picks every
    /// registration up automatically — no other wiring needed. Prefer deriving from
    /// <see cref="ActionType{TConfig}"/> over implementing <see cref="IActionType"/> directly:
    /// the base class wires typed config deserialization plus the resume / timeout hooks.
    /// </summary>
    public static IWorkflowCoreBuilder AddActionType<T>(this IWorkflowCoreBuilder builder)
        where T : class, IActionType
    {
        builder.Services.AddScoped<IActionType, T>();
        return builder;
    }

    // ===== Liquid extension hooks =====

    /// <summary>
    /// Optional consumer-supplied symmetric encryption hook for workflow PHI columns. Without
    /// this call, the engine writes plaintext UTF-8 bytes into the same <c>bytea</c> columns
    /// that would otherwise carry ciphertext — schema is unified, behaviour pivots only on
    /// presence of this registration. With it, every protected column is encrypted on write
    /// and decrypted on read; the key id used to seal each value is embedded in the ciphertext
    /// blob's wire format, so a re-encryption sweep after a key rotation inspects individual
    /// values rather than a per-row stamp. See <see cref="IWorkflowDataProtector"/> remarks for
    /// storage format, rotation strategy, and threading expectations.
    /// </summary>
    public static IWorkflowCoreBuilder AddWorkflowDataProtector<T>(this IWorkflowCoreBuilder builder)
        where T : class, IWorkflowDataProtector
    {
        // Singleton: protector holds the active key and (typically) a key ring. Same instance
        // services every DbContext / converter for the process lifetime.
        builder.Services.AddSingleton<IWorkflowDataProtector, T>();
        return builder;
    }

    /// <summary>
    /// Register a custom Liquid filter as a scoped service. The filter's <c>Name</c> is the token
    /// used in templates: <c>{{ value | name: arg }}</c>. The filter can take any other scoped
    /// service via constructor injection (DbContext, S3 client, etc.).
    /// <para>
    /// <b>Read <see cref="ILiquidFilter.InvokeAsync"/> remarks before implementing</b> — the
    /// filter body is a trust boundary; arguments come from untrusted workflow templates.
    /// </para>
    /// </summary>
    public static IWorkflowCoreBuilder AddLiquidFilter<T>(this IWorkflowCoreBuilder builder)
        where T : class, ILiquidFilter
    {
        builder.Services.AddScoped<ILiquidFilter, T>();
        return builder;
    }

    /// <summary>
    /// Register a per-evaluation Liquid hook that gets the Fluid <see cref="Fluid.TemplateOptions"/> /
    /// <see cref="Fluid.TemplateContext"/> plus the tenant-aware <see cref="ExpressionEvaluationContext"/>.
    /// Use for advanced setup beyond simple filters — value converters, MemberAccessStrategy
    /// registrations, custom globals, etc.
    /// <para>
    /// <b>Read <see cref="ILiquidExtension"/> remarks before implementing</b> — globals and
    /// member-access registrations you add are visible to the workflow author's untrusted
    /// template; pair carelessly and you create a confused-deputy attack surface.
    /// </para>
    /// </summary>
    public static IWorkflowCoreBuilder AddLiquidExtension<T>(this IWorkflowCoreBuilder builder)
        where T : class, ILiquidExtension
    {
        builder.Services.AddScoped<ILiquidExtension, T>();
        return builder;
    }

    // ===== JS extension hooks =====

    /// <summary>
    /// Register a custom JS function as a scoped service. <c>Name</c> is the identifier in JS code;
    /// <c>Create(evaluation)</c> returns the delegate to be set on the Jint engine. The function
    /// implementation can take any scoped service via constructor injection.
    /// <para>
    /// <b>Read <see cref="IJsFunction.Create"/> remarks before implementing</b> — the delegate
    /// body is a trust boundary; arguments come from untrusted workflow JS.
    /// </para>
    /// </summary>
    public static IWorkflowCoreBuilder AddJsFunction<T>(this IWorkflowCoreBuilder builder)
        where T : class, IJsFunction
    {
        builder.Services.AddScoped<IJsFunction, T>();
        return builder;
    }

    /// <summary>
    /// Register a per-evaluation JS hook that gets the freshly-built Jint engine plus the
    /// tenant-aware <see cref="ExpressionEvaluationContext"/>. Use for advanced setup beyond
    /// named functions — globals, scripts, custom marshalling, etc.
    /// <para>
    /// <b>Read <see cref="IJsExtension"/> remarks before implementing</b> — anything you put on
    /// the engine via <c>SetValue</c> is callable from untrusted workflow JS. Stick to
    /// primitives / frozen DTOs; route any host I/O through <see cref="IJsFunction"/> instead.
    /// </para>
    /// </summary>
    public static IWorkflowCoreBuilder AddJsExtension<T>(this IWorkflowCoreBuilder builder)
        where T : class, IJsExtension
    {
        builder.Services.AddScoped<IJsExtension, T>();
        return builder;
    }
}

/// <summary>
/// Marker for chained engine-related registrations (storage, triggers, action types, Liquid+JS
/// extensions). Keeps the call site readable and makes future extension methods discoverable.
/// </summary>
public interface IWorkflowCoreBuilder
{
    IServiceCollection Services { get; }
}

internal sealed class WorkflowCoreBuilder : IWorkflowCoreBuilder
{
    public WorkflowCoreBuilder(IServiceCollection services) => this.Services = services;

    public IServiceCollection Services { get; }
}
