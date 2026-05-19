namespace LayeredTemplate.App.Shared.Endpoints;

/// <summary>
/// Implemented by each endpoint class — discovery finds all <see cref="IEndpoint"/> implementers
/// in the assembly and invokes <see cref="Map"/> on each. Adding or removing an endpoint requires
/// no registration changes elsewhere.
/// </summary>
/// <remarks>
/// <para>The <c>app</c> argument is either the root <see cref="IEndpointRouteBuilder"/> or a
/// <see cref="RouteGroupBuilder"/> materialized from <see cref="IEndpointGroup"/> — depending on
/// whether the endpoint class carries <see cref="EndpointGroupAttribute{TGroup}"/>. In either case
/// the endpoint just calls <c>app.MapGet/MapPost(...)</c>; routing parents are transparent.</para>
/// <para>Convention: one endpoint = one file = one class. Endpoint classes live under
/// <c>Features/&lt;Feature&gt;/Endpoints/</c>. Their declaring class is <c>sealed</c>; all methods
/// are <c>static</c> — instances are never constructed.</para>
/// </remarks>
public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder app);
}
