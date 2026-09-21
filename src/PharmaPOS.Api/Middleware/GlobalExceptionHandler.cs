using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Api.Middleware;

/// <summary>
/// Translates unhandled exceptions into RFC 7807/9457 problem responses.
/// Known <see cref="DomainException"/>s surface a stable code and status; anything unexpected
/// becomes a generic 500 and is logged with full detail (internals never leak to clients).
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, problemDetails) = Map(exception, httpContext);

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    private static (int StatusCode, ProblemDetails ProblemDetails) Map(
        Exception exception,
        HttpContext httpContext)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (exception is ValidationException validationException)
        {
            return (StatusCodes.Status400BadRequest, new ValidationProblemDetails(
                validationException.Errors.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Extensions = { ["traceId"] = traceId, ["code"] = validationException.Code },
            });
        }

        if (exception is DomainException domainException)
        {
            var statusCode = domainException.Category switch
            {
                ErrorCategory.Validation => StatusCodes.Status400BadRequest,
                ErrorCategory.NotFound => StatusCodes.Status404NotFound,
                ErrorCategory.Conflict => StatusCodes.Status409Conflict,
                ErrorCategory.Forbidden => StatusCodes.Status403Forbidden,
                ErrorCategory.BusinessRule => StatusCodes.Status422UnprocessableEntity,
                ErrorCategory.Unauthorized => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status400BadRequest,
            };

            return (statusCode, new ProblemDetails
            {
                Status = statusCode,
                Title = TitleFor(domainException.Category),
                Detail = domainException.Message,
                Extensions = { ["traceId"] = traceId, ["code"] = domainException.Code },
            });
        }

        return (StatusCodes.Status500InternalServerError, new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Extensions = { ["traceId"] = traceId },
        });
    }

    private static string TitleFor(ErrorCategory category) => category switch
    {
        ErrorCategory.Validation => "One or more validation errors occurred.",
        ErrorCategory.NotFound => "The requested resource was not found.",
        ErrorCategory.Conflict => "The request conflicts with the current state of the resource.",
        ErrorCategory.Forbidden => "You are not permitted to perform this operation.",
        ErrorCategory.BusinessRule => "A business rule was violated.",
        ErrorCategory.Unauthorized => "Authentication is required.",
        _ => "The request could not be processed.",
    };
}
