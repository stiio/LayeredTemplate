using LayeredTemplate.App.Features.TodoLists.Services;
using LayeredTemplate.App.Shared.Auth;
using LayeredTemplate.App.Shared.Db;
using LayeredTemplate.App.Shared.Infrastructure.Email;
using LayeredTemplate.App.Shared.Infrastructure.Locks;

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
        services.AddScoped<AppDbContext>(_ => null!);
        services.AddSingleton<ILockProvider>(_ => null!);
        services.AddScoped<IEmailSender>(_ => null!);
        services.AddScoped<ITodoListRatingService>(_ => null!);
        services.AddScoped<ICurrentUserService>(_ => null!);

        return services;
    }
}
