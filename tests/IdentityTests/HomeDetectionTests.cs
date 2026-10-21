using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Safety.Application.Home.Detect;
using Zpi.Modules.Safety.Infrastructure.Geocoding;

namespace Zpi.IdentityTests;

public sealed partial class GeofenceTests
{
    [PostgresFact]
    public async Task Home_detection_checks_access_before_geocoding_caches_partial_addresses_and_does_not_save_home()
    {
        var provider = new GeocodingProvider();
        await using var factory = GeocodingFactory(provider);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var stranger = await Actor(factory, AccountRole.Caretaker);
        var relationship = await Link(senior, caretaker);
        var path = Base(senior.Id) + "/home-address/detect";
        var request = new DetectAddressRequest(52.2297, 21.0122);
        using var anonymous = factory.CreateClient();
        await Status(await anonymous.PostAsJsonAsync(path, request), 401);
        await Error(await stranger.Client.PostAsJsonAsync(path, request), 404, "resourceNotFound");
        await Status(await senior.Client.PostAsJsonAsync(path, new { latitude = 91, longitude = 21 }), 400);
        await Status(await senior.Client.PostAsJsonAsync(path, new { latitude = 52, longitude = -181 }), 400);
        await Status(await senior.Client.PostAsJsonAsync(path, new { latitude = 52 }), 400);
        Assert.Equal(0, provider.Calls);
        var response = await senior.Client.PostAsJsonAsync(path, request);
        await Status(response, 200);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var result = (await response.Content.ReadFromJsonAsync<DetectedAddressDto>())!;
        Assert.Equal("Warszawa, Polska", result.DisplayName);
        Assert.Equal("PL", result.Address.CountryCode);
        Assert.Equal("Warszawa", result.Address.City);
        Assert.Null(result.Address.HouseNumber);
        Assert.Equal(request.Latitude, result.Coordinates.Latitude);
        Assert.Equal(request.Longitude, result.Coordinates.Longitude);
        Assert.StartsWith("https://eu1.locationiq.com/v1/reverse?", provider.LastUri!);
        Assert.Contains("format=json&", provider.LastUri!);
        Assert.Contains("normalizeaddress=1", provider.LastUri!);
        Assert.Contains("key=pk.test%2B%26%3F%3D", provider.LastUri!);
        Assert.DoesNotContain("pk.test", await response.Content.ReadAsStringAsync());
        Assert.Contains("lat=52.2297", provider.LastUri!);
        Assert.Contains("lon=21.0122", provider.LastUri!);
        Assert.Equal("ZpiBackend/1.0", provider.LastUserAgent);
        await Status(await caretaker.Client.PostAsJsonAsync(path, request), 200);
        Assert.Equal(1, provider.Calls);
        await Status(await senior.Client.GetAsync(Base(senior.Id) + "/home-address"), 404);
        await Status(await caretaker.Client.DeleteAsync("/api/v1/care-relationships/" + relationship.RelationshipId), 204);
        await Error(await caretaker.Client.PostAsJsonAsync(path, request), 404, "resourceNotFound");
        Assert.Equal(1, provider.Calls);
    }

    [PostgresTheory]
    [InlineData(404, "{}", 404, "addressNotFound")]
    [InlineData(401, "Invalid key", 503, "temporarilyUnavailable")]
    [InlineData(403, "Access restricted", 503, "temporarilyUnavailable")]
    [InlineData(429, "{}", 503, "temporarilyUnavailable")]
    [InlineData(500, "private provider details", 503, "temporarilyUnavailable")]
    [InlineData(200, "not json", 503, "temporarilyUnavailable")]
    [InlineData(200, "{}", 503, "temporarilyUnavailable")]
    [InlineData(200, "{\"error\":\"provider failure\"}", 503, "temporarilyUnavailable")]
    public async Task Home_detection_translates_provider_errors_without_exposing_the_response(
        int providerStatus, string body, int status, string code)
    {
        var provider = new GeocodingProvider { Status = providerStatus, Body = body };
        await using var factory = GeocodingFactory(provider);
        using var senior = await Actor(factory, AccountRole.Senior);
        await Error(await senior.Client.PostAsJsonAsync(Base(senior.Id) + "/home-address/detect",
            new DetectAddressRequest(52, 21)), status, code);
        Assert.Equal(1, provider.Calls);
    }

    [PostgresFact]
    public async Task Home_detection_returns_unavailable_when_the_provider_is_not_configured()
    {
        var provider = new GeocodingProvider();
        await using var factory = GeocodingFactory(provider, "");
        using var senior = await Actor(factory, AccountRole.Senior);
        await Error(await senior.Client.PostAsJsonAsync(Base(senior.Id) + "/home-address/detect",
            new DetectAddressRequest(52, 21)), 503, "temporarilyUnavailable");
        Assert.Equal(0, provider.Calls);
    }

    [PostgresFact]
    public async Task Home_detection_handles_provider_timeouts()
    {
        var provider = new GeocodingProvider { Timeout = true };
        await using var factory = GeocodingFactory(provider);
        using var senior = await Actor(factory, AccountRole.Senior);
        await Error(await senior.Client.PostAsJsonAsync(Base(senior.Id) + "/home-address/detect",
            new DetectAddressRequest(52, 21)), 503, "temporarilyUnavailable");
    }

    private WebApplicationFactory<Program> GeocodingFactory(GeocodingProvider provider, string apiKey = "pk.test+&?=") =>
        Factory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Safety:Geocoding:Endpoint"] = "https://eu1.locationiq.com/v1/",
                    ["Safety:Geocoding:ApiKey"] = apiKey }));
            builder.ConfigureTestServices(services => services.AddHttpClient(LocationIqGeocoder.ClientName)
                .ConfigurePrimaryHttpMessageHandler(() => provider));
        });

    private sealed class GeocodingProvider : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public int Status { get; init; } = 200;
        public string Body { get; init; } = """{"display_name":"Warszawa, Polska","lat":"52.2","lon":"21.0","address":{"city":"Warszawa","country":"Polska","country_code":"pl"}}""";
        public bool Timeout { get; init; }
        public string? LastUri { get; private set; }
        public string? LastUserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri!.ToString();
            LastUserAgent = request.Headers.UserAgent.ToString();
            if (Timeout) throw new TaskCanceledException("Provider timed out.");
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)Status) { Content = new StringContent(Body) });
        }
    }
}
