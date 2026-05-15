namespace LayeredTemplate.App.Shared.Endpoints;

/// <summary>
/// Marks an <see cref="IEndpoint"/> as Development-only. <c>EndpointDiscovery.MapAllEndpoints</c>
/// skips classes carrying this attribute when the host environment is not Development, so the
/// endpoints never exist on the route table in staging/production — no possibility of accidental
/// exposure.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DevOnlyAttribute : Attribute;
