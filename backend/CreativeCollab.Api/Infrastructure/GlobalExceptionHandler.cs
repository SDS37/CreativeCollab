using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CreativeCollab.Api.Infrastructure;

/// <summary>Turns any unhandled exception into a problem-details response that reveals nothing about the failure.</summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        // Request binding failures (for example malformed JSON) are the caller's fault, not a server error.
        var status = exception is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError;

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        var problem = status >= StatusCodes.Status500InternalServerError
            ? new ProblemDetails { Status = status, Title = "An unexpected error occurred." }
            : new ProblemDetails { Status = status, Title = "The request could not be processed." };

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = null, // never hand the exception to the writer
            ProblemDetails = problem,
        });
    }
}
