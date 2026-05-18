namespace LayeredTemplate.App.Shared.Endpoints;

/// <summary>
/// Symmetric to <see cref="IEndpoint"/> but for DI registration: implement on a feature's
/// <c>_Routes.cs</c> (or any class living inside the feature folder) to register feature-internal
/// services in the container. <c>FeatureDiscovery.AddFeatureServices</c> finds all implementers
/// in the assembly and invokes <see cref="ConfigureServices"/> on each.
/// </summary>
/// <remarks>
/// <para>Use for services whose scope is a single feature (e.g. <c>ITodoListRatingService</c> only
/// consumed by TodoLists endpoints). Cross-cutting infrastructure (email, locks, telemetry) goes
/// to <c>Shared/Infrastructure/</c> and is registered directly in <c>Program.cs</c>.</para>
/// <para>Called <b>before</b> <c>builder.Build()</c>, so registrations participate in the DI
/// container that endpoints later resolve from. Idempotent runs are not guaranteed by discovery —
/// don't perform side-effects beyond <c>services.AddXxx</c> calls.</para>
/// </remarks>
public interface IFeatureServices
{
    static abstract void ConfigureServices(IServiceCollection services);
}
