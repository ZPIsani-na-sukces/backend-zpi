using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Zpi.Modules.Safety.Application;
using Zpi.Modules.Safety.Application.Home.Detect;

namespace Zpi.Modules.Safety.Infrastructure.Geocoding;

public sealed class LocationIqGeocoder(IHttpClientFactory clients, IOptions<GeocodingOptions> options) : IReverseGeocoder, IDisposable
{
    public const string ClientName = "safety-geocoding";
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 512 });
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextRequestAt;

    public async Task<SafetyResult<DetectedAddressDto>> DetectAsync(DetectAddressRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Endpoint) || string.IsNullOrWhiteSpace(options.Value.ApiKey)) return Unavailable();
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(request, out DetectedAddressDto? cached) && cached is not null)
                return SafetyResult<DetectedAddressDto>.Success(cached);
            var delay = nextRequestAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            nextRequestAt = DateTimeOffset.UtcNow.AddSeconds(1);
            using var client = clients.CreateClient(ClientName);
            var endpoint = new Uri(options.Value.Endpoint.TrimEnd('/') + "/reverse");
            var query = string.Create(CultureInfo.InvariantCulture,
                $"?format=json&addressdetails=1&normalizeaddress=1&accept-language=pl&lat={request.Latitude:R}&lon={request.Longitude:R}&key={Uri.EscapeDataString(options.Value.ApiKey)}");
            using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, query));
            message.Headers.UserAgent.ParseAdd(options.Value.UserAgent);
            using var response = await client.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return SafetyResult<DetectedAddressDto>.Failed(SafetyFailure.AddressNotFound);
            if (!response.IsSuccessStatusCode) return Unavailable();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Unavailable();
            if (root.TryGetProperty("error", out _)) return Unavailable();
            if (!root.TryGetProperty("display_name", out var display) || display.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(display.GetString()) ||
                !root.TryGetProperty("address", out var address) || address.ValueKind != JsonValueKind.Object) return Unavailable();
            var countryCode = Read(address, "country_code")?.ToUpperInvariant();
            if (countryCode is not null && (countryCode.Length != 2 || countryCode.Any(letter => letter is < 'A' or > 'Z')))
                return Unavailable();
            var result = new DetectedAddressDto(display.GetString()!, new(request.Latitude, request.Longitude),
                new(Read(address, "road"), Read(address, "house_number"), Read(address, "postcode"),
                    Read(address, "city"), Read(address, "town"), Read(address, "village"), Read(address, "state"),
                    Read(address, "country"), countryCode));
            cache.Set(request, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15), Size = 1 });
            return SafetyResult<DetectedAddressDto>.Success(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Unavailable(); }
        catch (HttpRequestException) { return Unavailable(); }
        catch (JsonException) { return Unavailable(); }
        finally { gate.Release(); }
    }

    private static string? Read(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static SafetyResult<DetectedAddressDto> Unavailable() =>
        SafetyResult<DetectedAddressDto>.Failed(SafetyFailure.GeocodingUnavailable);

    public void Dispose()
    {
        cache.Dispose();
        gate.Dispose();
    }
}
