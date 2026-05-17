using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SafeRideKids.Biometria.Api.ExceptionHandling;

/// <summary>
/// Handler global que devolve ProblemDetails (RFC 7807) para qualquer exceção
/// não tratada. Nunca expõe stack trace ao cliente.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception path={Path}", httpContext.Request.Path);

        var (status, title) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid argument"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            NotSupportedException => (StatusCodes.Status501NotImplemented, "Not supported"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Invalid operation"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error"),
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path,
            Type = $"https://saferidekids.example/errors/{exception.GetType().Name.ToLowerInvariant()}"
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
