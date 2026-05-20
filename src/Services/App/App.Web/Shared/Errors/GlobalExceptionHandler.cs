using LayeredTemplate.App.Shared.Errors.Exceptions;
using LayeredTemplate.App.Shared.Errors.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace LayeredTemplate.App.Shared.Errors;

/// <summary>
/// Single funnel that turns every unhandled exception into an RFC 7807
/// <see cref="AppProblemDetails"/> response. App-specific exceptions
/// (<see cref="AppMessageException"/>, <see cref="AppValidationException"/>, <see cref="DomainException"/>)
/// map to 4xx with meaningful detail; everything else gets a generic 500 with no internal info
/// leaked to the caller — the stack trace lives in the log via <c>logger.LogError</c>.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> logger;
    private readonly IProblemDetailsService problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        this.logger = logger;
        this.problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = new AppProblemDetails { Instance = httpContext.Request.Path };

        this.MapException(exception, problemDetails);

        httpContext.Response.StatusCode = problemDetails.Status ?? 500;
        await this.problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });

        return true;
    }

    private void MapException(Exception exception, AppProblemDetails problemDetails)
    {
        switch (exception)
        {
            case AppMessageException messageException:
                this.logger.LogInformation("Message exception stack trace: {StackTrace}", messageException.StackTrace);
                problemDetails.Status = 400;
                problemDetails.Title = messageException.Message;
                problemDetails.ErrorType = messageException.ErrorType;
                problemDetails.Detail = messageException.Details;
                break;

            case AppValidationException validationException:
                problemDetails.Status = 400;
                problemDetails.Errors = validationException.Errors;
                problemDetails.Title = "One or more validation errors occurred.";
                problemDetails.ErrorType = AppErrorType.ValidationError;
                break;

            case DomainException domainException:
                this.logger.LogError(exception, "Unhandled exception occurred.");
                problemDetails.Status = 400;
                problemDetails.Title = domainException.Message;
                problemDetails.ErrorType = AppErrorType.DomainError;
                break;

            case NotSupportedException:
                this.logger.LogError(exception, "Unhandled exception occurred.");
                problemDetails.Status = 400;
                problemDetails.Title = "Not supported.";
                problemDetails.ErrorType = AppErrorType.NotSupported;
                break;

            case NotImplementedException:
                this.logger.LogError(exception, "Unhandled exception occurred.");
                problemDetails.Status = 501;
                problemDetails.Title = "Not implemented.";
                problemDetails.Detail = "This feature is not yet available.";
                problemDetails.ErrorType = AppErrorType.NotImplemented;
                break;

            default:
                this.logger.LogError(exception, "Unhandled exception occurred.");
                problemDetails.Status = 500;
                problemDetails.Title = "Internal Server Error.";
                problemDetails.ErrorType = AppErrorType.InternalServerError;
                break;
        }
    }
}
