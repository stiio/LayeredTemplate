namespace LayeredTemplate.App.Shared.Validation;

public static class ValidationFilterExtensions
{
    /// <summary>
    /// Attaches <see cref="ValidationFilter{T}"/> to this endpoint. Use after <c>MapPost</c> /
    /// <c>MapPut</c> / etc., e.g. <c>group.MapPost("/", Handle).WithValidation&lt;CreateFoo.Request&gt;()</c>.
    /// </summary>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>();
}