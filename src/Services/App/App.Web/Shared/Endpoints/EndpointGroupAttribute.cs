namespace LayeredTemplate.App.Shared.Endpoints;

/// <summary>
/// Links an <see cref="IEndpoint"/> to a route group declared by <typeparamref name="TGroup"/>.
/// During discovery, the endpoint's <see cref="IEndpoint.Map"/> receives the
/// <see cref="RouteGroupBuilder"/> created by <typeparamref name="TGroup"/> instead of the root
/// route builder. Omit the attribute to register directly on the root builder (no group).
/// </summary>
/// <typeparam name="TGroup">A class implementing <see cref="IEndpointGroup"/>.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EndpointGroupAttribute<TGroup> : Attribute
    where TGroup : IEndpointGroup;
