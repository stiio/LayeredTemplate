using FluentValidation;
using LayeredTemplate.Application.Common.Exceptions;
using MediatR;

namespace LayeredTemplate.Application.Common.Behaviors;

internal class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        this.validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (this.validators.Any())
        {
            var validationResults = await Task.WhenAll(
                this.validators.Select(v =>
                    v.ValidateAsync(request, cancellationToken)));

            var errors = validationResults
                .Where(r => r.Errors.Any())
                .SelectMany(r => r.Errors)
                .Select(error => error.ErrorMessage)
                .ToArray();

            if (errors.Any())
            {
                var exceptionMessage = string.Join("\n", errors);
                throw new HttpStatusException(exceptionMessage);
            }
        }

        return await next();
    }
}