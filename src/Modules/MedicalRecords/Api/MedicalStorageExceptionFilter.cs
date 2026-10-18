using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Zpi.Modules.MedicalRecords.Application.Files;

namespace Zpi.Modules.MedicalRecords.Api;

public sealed class MedicalStorageExceptionFilter(ILogger<MedicalStorageExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not MedicalStorageException) return;
        logger.LogError(context.Exception, "Medical storage operation failed.");
        context.HttpContext.Response.Headers.RetryAfter = "5";
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = 503,
            Detail = "File storage is temporarily unavailable.",
            Extensions = { ["code"] = "temporarilyUnavailable" }
        })
        { StatusCode = 503 };
        context.ExceptionHandled = true;
    }
}
