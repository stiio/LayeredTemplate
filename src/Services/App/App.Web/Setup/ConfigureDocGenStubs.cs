using LayeredTemplate.App.Shared.Db;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.App.Setup;

/// <summary>
/// Type-only registrations for services that endpoint handlers resolve from DI, used <b>only</b>
/// by the <c>GetDocument.Insider</c> build-time branch in <c>Program.cs</c>.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> Minimal API's request-delegate factory inspects every handler's
/// parameters at endpoint-build time and asks <see cref="IServiceProviderIsService"/> whether each
/// parameter type is a registered service. If it isn't, binding falls back to body / route /
/// query inference and may throw <c>"Failure to infer one or more parameters"</c>. During
/// <c>dotnet build</c>'s OpenAPI document generation we don't want the full production registration
/// (real Postgres connection, DataProtection wired to a real DB, startup tasks running migrations)
/// — but we still need the types to be visible to the service provider so endpoint signatures
/// resolve cleanly.</para>
///
/// <para><b>What's in here.</b> Lightweight <i>type-only</i> registrations. Stubs are constructed
/// (well — registered as services that <i>could</i> be constructed) but the doc generator never
/// invokes handlers, so the stubs are never actually resolved. Side-effects (connections, IO) do
/// not fire.</para>
///
/// <para><b>How to maintain.</b> When a new DI-resolved service appears in some endpoint's
/// handler signature and <c>dotnet build</c> fails with <i>Failure to infer one or more
/// parameters</i> during the OpenAPI doc-gen step — add a stub for that service here. One line.
/// The production-side registrations (<c>AddAppDb</c>, etc.) stay untouched and free of
/// build-time conditionals.</para>
/// </remarks>
public static class ConfigureDocGenStubs
{
    public static IServiceCollection AddDocGenStubs(this IServiceCollection services)
    {
        // EF Core AppDbContext stub. UseNpgsql is called with a fake connection string;
        // Npgsql doesn't attempt to connect until the first command. The doc generator never
        // runs commands — it only walks endpoint metadata. UseSnakeCaseNamingConvention is
        // preserved because model-building (lazy on first use) may invoke it if the model is
        // ever built, and we'd rather build it correctly than not at all.
        services.AddDbContextPool<AppDbContext>(options =>
            options.UseNpgsql("Host=stub").UseSnakeCaseNamingConvention());

        // Add more stubs below as endpoint signatures grow. Examples:
        //
        //   services.AddSingleton<ILockProvider>(_ => null!);
        //   services.AddScoped<IEmailSender>(_ => null!);
        //
        // For null-returning factories: minimal API still sees the type as registered (via
        // IServiceProviderIsService), and since the doc generator never resolves them, the
        // null is harmless. If you ever need the doc generator to actually USE the service
        // (rare — only if a metadata transformer reaches into DI), provide a real no-op
        // implementation instead.

        return services;
    }
}
