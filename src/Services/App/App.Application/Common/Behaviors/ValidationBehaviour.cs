using FluentValidation;
using LayeredTemplate.App.Application.Common.Exceptions;
using Mediator;

namespace LayeredTemplate.App.Application.Common.Behaviors;

internal class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    private readonly IEnumerable<IValidator<TRequest>> validators;

    public ValidationBehaviour(
        IEnumerable<IValidator<TRequest>> validators)
    {
        this.validators = validators;
    }

    public async ValueTask<TResponse> Handle(TRequest message, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        if (!this.validators.Any())
        {
            return await next(message, cancellationToken);
        }

        var errors = this.validators
            .Select(x => x.Validate(message))
            .SelectMany(x => x.Errors)
            .Where(x => x != null)
            .GroupBy(
                x => x.PropertyName,
                x => x.ErrorMessage,
                (propertyName, errorMessages) => new
                {
                    Key = propertyName,
                    Values = errorMessages.Distinct().ToArray(),
                })
            .ToDictionary(x => x.Key, x => x.Values);

        if (errors.Any())
        {
            throw new AppValidationException(errors);
        }

        return await next(message, cancellationToken);
    }
}