using System.Text.Json;
using FluentValidation.Results;

namespace Zpi.Api.ErrorHandling;

internal static class ValidationErrors
{
    public static Dictionary<string, string[]> FromFailures(IEnumerable<ValidationFailure> failures) => failures
        .GroupBy(failure => FieldName(failure.PropertyName))
        .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

    public static string FieldName(string propertyPath) => string.Join('.',
        propertyPath.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
