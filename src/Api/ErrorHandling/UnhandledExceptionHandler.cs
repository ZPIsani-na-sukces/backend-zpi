using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Zpi.Api.ErrorHandling;

internal sealed class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (httpContext.RequestAborted.IsCancellationRequested) return true;
        if (exception is BadHttpRequestException { StatusCode: >= 400 and < 500 } badRequest)
        {
            httpContext.Response.StatusCode = badRequest.StatusCode;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Detail = "The request could not be read."
                }
            });
            return true;
        }
        logger.LogError(exception, "Unhandled request error. TraceId: {TraceId}, Method: {Method}, Path: {Path}",
            httpContext.TraceIdentifier, httpContext.Request.Method, httpContext.Request.Path);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An unexpected error occurred.",
                Extensions = { ["code"] = "internalServerError" }
            }
        });
        return true;
    }
}
