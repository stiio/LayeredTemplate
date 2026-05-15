using FluentValidation;
using LayeredTemplate.App.Shared.Errors;

namespace LayeredTemplate.App.Shared.Validation;

/// <summary>
/// Endpoint filter that auto-validates a typed request via the registered
/// <see cref="IValidator{T}"/> before the handler body runs. Failures throw
/// <see cref="AppValidationException"/>, which <see cref="GlobalExceptionHandler"/>
/// turns into RFC 7807 400 with per-field <c>errors</c>.
/// </summary>
/// <remarks>
/// Replaces the MediatR-style pipeline behaviour from the old architecture. The benefit of
/// using an endpoint filter is locality: validation attaches to specific endpoints (visible in
/// the registration call), not all of them at once.
/// </remarks>
public sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    private readonly IValidator<T> validator;

    public ValidationFilter(IValidator<T> validator)
    {
        this.validator = validator;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // First parameter of type T — minimal API binding produces it from the [FromBody] / [FromRoute] / etc.
        var instance = context.Arguments.OfType<T>().FirstOrDefault();
        if (instance is null)
        {
            return await next(context);
        }

        var result = await this.validator.ValidateAsync(instance, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        var errors = result.Errors
            .GroupBy(x => x.PropertyName, x => x.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.Distinct().ToArray());

        throw new AppValidationException(errors);
    }
}

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
