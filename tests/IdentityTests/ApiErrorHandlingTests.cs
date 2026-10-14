using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation;
using Npgsql;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Api.ErrorHandling;

namespace Zpi.IdentityTests;

public sealed class ApiErrorHandlingTests
{
    [Fact]
    public async Task Validation_errors_include_all_fields_and_skip_action()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe", new { name = "", age = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validationFailed", body.RootElement.GetProperty("code").GetString());
        var errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("age", out _));
    }

    [Fact]
    public async Task Valid_request_reaches_action()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe", new { name = "Jan", age = 70 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_json_uses_the_same_validation_contract()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();
        using var content = new StringContent("{", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/probe", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Expected depth", json);
        using var body = JsonDocument.Parse(json);
        Assert.Equal("validationFailed", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Object, body.RootElement.GetProperty("errors").ValueKind);
        Assert.Single(body.RootElement.EnumerateObject(), property => property.Name == "errors");
    }

    [Fact]
    public async Task Null_body_returns_validation_problem()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();
        using var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/probe", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validationFailed", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unknown_route_returns_contract_problem()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("resourceNotFound", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unexpected_exception_returns_safe_problem()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/probe/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-exception-marker", json);
        using var body = JsonDocument.Parse(json);
        Assert.Equal("internalServerError", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
        Assert.True(body.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Bad_http_request_keeps_its_status()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/probe/bad-request");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sensitive-request-marker", json);
        using var body = JsonDocument.Parse(json);
        Assert.Equal("payloadTooLarge", body.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("validation-exception", 400, "validationFailed")]
    [InlineData("concurrency", 409, "invalidState")]
    [InlineData("problem", 409, "invalidState")]
    [InlineData("not-found", 404, "resourceNotFound")]
    [InlineData("duplicate", 409, "invalidState")]
    [InlineData("database-error", 500, "internalServerError")]
    [InlineData("serialization", 409, "invalidState")]
    [InlineData("deadlock", 409, "invalidState")]
    public async Task Errors_from_handlers_and_mvc_use_the_shared_contract(string path, int status, string code)
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();

        var response = await client.GetAsync($"/probe/{path}");

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("sensitive-database-marker", body.RootElement.ToString());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(status, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal($"/probe/{path}", body.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Concurrency_conflict_with_if_match_returns_precondition_failed()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", "\"1\"");

        var response = await client.GetAsync("/probe/concurrency");

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sensitive-concurrency-marker", json);
        using var body = JsonDocument.Parse(json);
        Assert.Equal("versionMismatch", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Module_specific_problem_extensions_are_preserved_without_duplicate_fields()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/probe/extensions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("phoneVerificationRequired", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("test-ticket", body.RootElement.GetProperty("verificationTicket").GetString());
        Assert.Single(body.RootElement.EnumerateObject(), property => property.Name == "code");
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("errors").ValueKind);
    }

    [Fact]
    public async Task Validation_exception_normalizes_nested_fields_and_removes_duplicate_messages()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/probe/validation-exception");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var messages = body.RootElement.GetProperty("errors").GetProperty("contacts[0].phoneNumber");
        Assert.Single(messages.EnumerateArray());
        Assert.Equal("Invalid phone number.", messages[0].GetString());
    }

    [Fact]
    public async Task Asynchronous_validators_from_another_feature_are_discovered()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe/async", new { name = "rejected", age = 70 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("name", out _));
    }

    private static async Task<WebApplication> CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddApiErrorHandling();
        builder.Services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
        builder.Services.AddScoped<IValidator<ProbeRequest>, ProbeRequestValidator>();
        builder.Services.AddScoped<IValidator<AsyncProbeRequest>, AsyncProbeRequestValidator>();
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }
}

public sealed record ProbeRequest(string Name, int Age);

public sealed class ProbeRequestValidator : AbstractValidator<ProbeRequest>
{
    public ProbeRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty();
        RuleFor(request => request.Age).GreaterThan(0);
    }
}

public sealed record AsyncProbeRequest(string Name, int Age);

public sealed class AsyncProbeRequestValidator : AbstractValidator<AsyncProbeRequest>
{
    public AsyncProbeRequestValidator()
    {
        RuleFor(request => request.Name).MustAsync(async (name, cancellationToken) =>
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return name != "rejected";
        });
    }
}

[ApiController]
[Route("probe")]
public sealed class ProbeController : ControllerBase
{
    [HttpPost]
    public IActionResult Validate(ProbeRequest request) => Ok();

    [HttpPost("async")]
    public IActionResult ValidateAsync(AsyncProbeRequest request) => Ok();

    [HttpGet("validation-exception")]
    public IActionResult ThrowValidation() => throw new ValidationException(
    [
        new ValidationFailure("Contacts[0].PhoneNumber", "Invalid phone number."),
        new ValidationFailure("Contacts[0].PhoneNumber", "Invalid phone number.")
    ]);

    [HttpGet("duplicate")]
    public IActionResult ThrowDuplicate() => throw new DbUpdateException("sensitive-database-marker",
        new PostgresException("sensitive-database-marker", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));

    [HttpGet("database-error")]
    public IActionResult ThrowDatabaseError() => throw new DbUpdateException("sensitive-database-marker",
        new PostgresException("sensitive-database-marker", "ERROR", "ERROR", PostgresErrorCodes.CheckViolation));

    [HttpGet("serialization")]
    public IActionResult ThrowSerialization() => throw new InvalidOperationException("sensitive-database-marker",
        new DbUpdateException("sensitive-database-marker",
            new PostgresException("sensitive-database-marker", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure)));

    [HttpGet("deadlock")]
    public IActionResult ThrowDeadlock() => throw new PostgresException("sensitive-database-marker", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected);

    [HttpGet("concurrency")]
    public IActionResult ThrowConcurrency() => throw new DbUpdateConcurrencyException("sensitive-concurrency-marker");

    [HttpGet("problem")]
    public IActionResult BusinessProblem() => Problem(statusCode: 409, detail: "The operation is unavailable.");

    [HttpGet("extensions")]
    public IActionResult ExtendedProblem() => Problem(statusCode: 403,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "phoneVerificationRequired",
            ["verificationTicket"] = "test-ticket"
        });

    [HttpGet("not-found")]
    public IActionResult MissingResource() => NotFound();

    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException("secret-exception-marker");

    [HttpGet("bad-request")]
    public IActionResult ThrowBadRequest() => throw new BadHttpRequestException("sensitive-request-marker", 413);
}
