using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Zpi.Api.ErrorHandling;

internal sealed class ExpectedExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case ValidationException validation:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Invalid request data.",
                    Extensions =
                    {
                        ["code"] = "validationFailed",
                        ["errors"] = ValidationErrors.FromFailures(validation.Errors)
                    }
                };
                break;
            case DbUpdateConcurrencyException:
                var hasPrecondition = !string.IsNullOrWhiteSpace(httpContext.Request.Headers.IfMatch);
                problem = new ProblemDetails
                {
                    Status = hasPrecondition ? StatusCodes.Status412PreconditionFailed : StatusCodes.Status409Conflict,
                    Detail = "The resource has changed. Reload it before retrying.",
                    Extensions = { ["code"] = hasPrecondition ? "versionMismatch" : "invalidState" }
                };
                break;
            case DbUpdateException update when update.InnerException is PostgresException postgres &&
                postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Detail = "The operation conflicts with an existing resource.",
                    Extensions = { ["code"] = "invalidState" }
                };
                break;
            default:
                return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        });
        return true;
    }
}
