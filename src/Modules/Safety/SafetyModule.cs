using Microsoft.Extensions.Configuration;
using Zpi.Modules.Safety.Application.Home.Detect;
using Zpi.Modules.Safety.Infrastructure.Geocoding;
using Zpi.Modules.Safety.Application.Alerts;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Safety.Api;
using Zpi.Modules.Safety.Application;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety;

public static class SafetyModule
{
    public static IServiceCollection AddSafetyModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GeocodingOptions>().Bind(configuration.GetSection(GeocodingOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) &&
                endpoint.Scheme == "https" && endpoint.IsDefaultPort &&
                endpoint.Host is "eu1.locationiq.com" or "us1.locationiq.com" &&
                endpoint.AbsolutePath.TrimEnd('/') == "/v1" && string.IsNullOrEmpty(endpoint.Query) &&
                string.IsNullOrEmpty(endpoint.Fragment) && string.IsNullOrEmpty(endpoint.UserInfo),
                "Safety:Geocoding:Endpoint must be https://eu1.locationiq.com/v1/ or https://us1.locationiq.com/v1/.")
            .Validate(options => options.ApiKey.Length <= 4096 && !options.ApiKey.Any(char.IsWhiteSpace),
                "Safety:Geocoding:ApiKey must not contain whitespace or exceed 4096 characters.")
            .Validate(options => System.Net.Http.Headers.ProductInfoHeaderValue.TryParse(options.UserAgent, out _),
                "Safety:Geocoding:UserAgent must identify the application, for example ZpiBackend/1.0.")
            .ValidateOnStart();
        services.AddHttpClient(LocationIqGeocoder.ClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.MaxResponseContentBufferSize = 65_536;
        }).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<IReverseGeocoder, LocationIqGeocoder>();
        services.AddOptions<AlertOptions>()
            .Bind(configuration.GetSection(AlertOptions.SectionName))
            .Validate(options => options.Freshness > TimeSpan.Zero, "Safety:Alerts:Freshness must be positive.")
            .Validate(options => options.Confirmation > TimeSpan.Zero && options.Confirmation <= options.Freshness,
                "Safety:Alerts:Confirmation must be positive and no greater than Freshness.")
            .Validate(options => options.LowBatteryPercent is >= 0 and < 100 &&
                options.BatteryRearmPercent > options.LowBatteryPercent && options.BatteryRearmPercent <= 100,
                "Safety:Alerts battery thresholds must satisfy 0 <= LowBatteryPercent < BatteryRearmPercent <= 100.")
            .ValidateOnStart();
        services.AddSafetyApi();
        services.AddSafetyApplication();
        services.AddSafetyPersistence(configuration);
        return services;
    }
}
