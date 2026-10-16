using System.Text.Json;
using Zpi.Modules.Safety.Domain;

namespace Zpi.Modules.Safety.Application.Alerts;

internal static class GeofenceDetector
{
    public static bool Evaluate(AutomaticAlertState state, LocationSample sample, IReadOnlyList<GeoZone> zones, DateTimeOffset now, AlertOptions options)
    {
        if (!options.IsFresh(sample.CapturedAt, now) || sample.CapturedAt <= state.LocationEvaluatedAt) return false;
        if (state.LocationEvaluatedAt is { } previous && sample.CapturedAt - previous > options.Freshness) state.ResetGeofence();
        state.LocationEvaluatedAt = sample.CapturedAt;
        if (zones.Count == 0)
        {
            state.ResetGeofence();
            return false;
        }
        var inside = new List<GeoZone>();
        var outsideAll = true;
        if (sample.AccuracyMeters is { } accuracy)
        {
            foreach (var zone in zones)
            {
                var distance = Distance(sample.Latitude, sample.Longitude, zone.CenterLatitude, zone.CenterLongitude);
                if (distance + accuracy < zone.RadiusMeters) inside.Add(zone);
                if (distance <= zone.RadiusMeters + accuracy) outsideAll = false;
            }
        }
        else outsideAll = false;
        bool? observation = inside.Count > 0 ? true : outsideAll ? false : null;
        if (observation is null)
        {
            state.CandidateInside = null;
            state.CandidateSince = null;
            return false;
        }
        if (observation == true && state.IsInside == true)
            state.InsideZonesJson = Snapshot(inside);
        if (observation == state.IsInside)
        {
            state.CandidateInside = null;
            state.CandidateSince = null;
            return false;
        }
        if (state.CandidateInside != observation)
        {
            state.CandidateInside = observation;
            state.CandidateSince = sample.CapturedAt;
            if (observation == true && state.IsInside is null) state.InsideZonesJson = Snapshot(inside);
            return false;
        }
        if (sample.CapturedAt - state.CandidateSince < options.Confirmation) return false;
        var exited = state.IsInside == true && observation == false;
        state.IsInside = observation;
        state.CandidateInside = null;
        state.CandidateSince = null;
        if (observation == true)
        {
            state.InsideZonesJson = Snapshot(inside);
            state.GeofenceNotificationActive = false;
        }
        return exited;
    }

    private static string Snapshot(IEnumerable<GeoZone> zones) => JsonSerializer.Serialize(zones.OrderBy(row => row.Id)
        .Select(row => new DepartedZoneDto(row.Id, row.Name, row.SmsOnExitEnabled)));

    private static double Distance(double latitude, double longitude, double centerLatitude, double centerLongitude)
    {
        const double earthRadiusMeters = 6_371_000;
        var radians = Math.PI / 180;
        var latitudeDelta = (latitude - centerLatitude) * radians;
        var longitudeDelta = (longitude - centerLongitude) * radians;
        var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2) + Math.Cos(latitude * radians) *
            Math.Cos(centerLatitude * radians) * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return earthRadiusMeters * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
}
