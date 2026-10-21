namespace Zpi.Modules.Safety.Infrastructure.Geocoding;

public sealed class GeocodingOptions
{
    public const string SectionName = "Safety:Geocoding";
    public string Endpoint { get; set; } = "https://eu1.locationiq.com/v1/";
    public string ApiKey { get; set; } = "";
    public string UserAgent { get; set; } = "ZpiBackend/1.0";
}
