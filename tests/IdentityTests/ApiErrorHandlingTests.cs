using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation;
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

    private static async Task<WebApplication> CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddApiErrorHandling();
        builder.Services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
        builder.Services.AddScoped<IValidator<ProbeRequest>, ProbeRequestValidator>();
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

[ApiController]
[Route("probe")]
public sealed class ProbeController : ControllerBase
{
    [HttpPost]
    public IActionResult Validate(ProbeRequest request) => Ok();

    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException("secret-exception-marker");

    [HttpGet("bad-request")]
    public IActionResult ThrowBadRequest() => throw new BadHttpRequestException("sensitive-request-marker", 413);
}
