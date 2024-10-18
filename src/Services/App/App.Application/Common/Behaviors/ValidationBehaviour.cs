using FluentValidation;
using FluentValidation.Results;
using LayeredTemplate.App.Application.Common.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.App.Application.Common.Behaviors;

internal class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> validators;
    private readonly ILogger<ValidationBehaviour<TRequest, TResponse>> logger;

    public ValidationBehaviour(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehaviour<TRequest, TResponse>> logger)
    {
        this.validators = validators;
        this.logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!this.validators.Any())
        {
            return await next();
        }

        var validationResults = new List<ValidationResult>();
        foreach (var validator in this.validators)
        {
            this.logger.LogInformation($"Validator process: {validator.GetType().Name}");
            var result = await validator.ValidateAsync(request, cancellationToken);
            validationResults.Add(result);
        }

        var errors = validationResults
            .Where(r => r.Errors.Any())
            .SelectMany(r => r.Errors)
            .Select(error => error.ErrorMessage)
            .ToArray();

        if (!errors.Any())
        {
            return await next();
        }

        var exceptionMessage = string.Join("\n", errors);
        throw new HttpStatusException(exceptionMessage);
    }
}