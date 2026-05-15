namespace LayeredTemplate.App.Shared.Endpoints;

/// <summary>
/// Marker interface implemented by every feature's route registration entry-point.
/// <c>EndpointDiscovery.MapAllEndpoints</c> finds all <see cref="IEndpoint"/>
/// implementers in the assembly and invokes <see cref="Map"/> on each, removing the need
/// to maintain a manual list of feature wires in <c>Program.cs</c>.
/// </summary>
/// <remarks>
/// Convention in this codebase: one <c>_Routes.cs</c> file per feature implements this
/// interface and registers the feature's grouped endpoints (see e.g. <c>Features/Info/_Routes.cs</c>).
/// Individual endpoint classes (e.g. <c>GetInfo</c>) expose a <c>Configure(RouteGroupBuilder)</c>
/// method called from the feature's <see cref="Map"/> — they are NOT <see cref="IEndpoint"/>
/// themselves, so adding/removing endpoints inside a feature doesn't change <c>Program.cs</c>.
/// </remarks>
public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder app);
}
