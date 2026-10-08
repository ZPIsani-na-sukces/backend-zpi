using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Zpi.Api.ErrorHandling;

internal sealed class ProblemDetailsFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: ProblemDetails problem } result) return;
        problem.Status ??= result.StatusCode ?? StatusCodes.Status500InternalServerError;
        result.Value = ApiProblemResponse.FromProblemDetails(context.HttpContext, problem);
        result.DeclaredType = typeof(ApiProblemResponse);
        result.StatusCode = problem.Status;
        result.ContentTypes.Clear();
        result.ContentTypes.Add("application/problem+json");
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
