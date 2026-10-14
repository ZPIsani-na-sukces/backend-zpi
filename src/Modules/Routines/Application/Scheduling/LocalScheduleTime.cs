namespace Zpi.Modules.Routines.Application.Scheduling;

public static class LocalScheduleTime
{
    public static bool IsValidZone(string? id) => !string.IsNullOrWhiteSpace(id) &&
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) && (id == "UTC" || zone.HasIanaId);

    public static DateTimeOffset Resolve(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
            local += zone.GetUtcOffset(local.AddDays(1)) - zone.GetUtcOffset(local.AddDays(-1));
        if (zone.IsAmbiguousTime(local))
            return new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).ToUniversalTime();
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }
}
