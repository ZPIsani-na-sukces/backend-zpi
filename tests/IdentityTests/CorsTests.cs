using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class CorsTests(IdentityApiFixture fixture)
{
    [PostgresFact]
    public async Task Allowed_origin_receives_cors_headers_on_preflight_and_validation_errors()
    {
        await using var factory = fixture.Factory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Authentication:AllowedWebOrigins:0"] = "http://localhost:5173" })));
        using var client = factory.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/password-resets/verify");
        preflight.Headers.Add("Origin", "http://localhost:5173");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
        var response = await client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        using var invalid = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/password-resets/verify")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        invalid.Headers.Add("Origin", "http://localhost:5173");
        response = await client.SendAsync(invalid);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        using var denied = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/password-resets/verify");
        denied.Headers.Add("Origin", "https://untrusted.example");
        denied.Headers.Add("Access-Control-Request-Method", "POST");
        response = await client.SendAsync(denied);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
