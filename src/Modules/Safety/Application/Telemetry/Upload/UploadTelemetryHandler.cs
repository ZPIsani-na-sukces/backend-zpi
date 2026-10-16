using System.Data;
using FluentValidation;
using Zpi.Modules.Safety.Application.Alerts;
using Zpi.Modules.Notifications.Contracts;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Telemetry.Upload;

public sealed class UploadTelemetryHandler(SafetyDbContext context, ICareFacade care,
    IValidator<TelemetrySample> validator, TimeProvider clock, AutomaticAlertProcessor alerts, INotificationProcessingSignal signal)
{
    public async Task<SafetyResult<TelemetryBatchResultDto>> Handle(UploadTelemetryCommand command, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(command.AccountId, command.AccountId, cancellationToken))
            return SafetyResult<TelemetryBatchResultDto>.Failed(SafetyFailure.OperationForbidden);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var device = await context.Devices.SingleOrDefaultAsync(row => row.Id == command.DeviceId &&
            row.AssignedAccountId == command.AccountId && row.RetiredAt == null, cancellationToken);
        if (device is null) return SafetyResult<TelemetryBatchResultDto>.Failed(SafetyFailure.ResourceNotFound);
        if (!device.MonitoringEnabled) return SafetyResult<TelemetryBatchResultDto>.Failed(SafetyFailure.MonitoringDisabled);

        var now = clock.GetUtcNow();
        var results = new TelemetrySampleResultDto[command.Samples.Count];
        var valid = new List<(int Index, TelemetrySample Sample)>();
        for (var index = 0; index < command.Samples.Count; index++)
        {
            var sample = command.Samples[index];
            if (sample is null)
            {
                results[index] = new("rejected", "invalidSample");
                continue;
            }
            var validation = await validator.ValidateAsync(sample, cancellationToken);
            if (!validation.IsValid) results[index] = new("rejected", validation.Errors[0].ErrorCode);
            else valid.Add((index, sample with
            {
                CapturedAt = DateTimeOffset.FromUnixTimeMilliseconds(sample.CapturedAt.ToUnixTimeMilliseconds())
            }));
        }

        var capturedTimes = valid.Select(item => item.Sample.CapturedAt).Distinct().ToArray();
        var locations = (await context.LocationSamples
            .Where(row => row.DeviceId == device.Id && capturedTimes.Contains(row.CapturedAt))
            .Select(row => row.CapturedAt)
            .ToListAsync(cancellationToken)).ToHashSet();
        var batteries = (await context.BatterySamples
            .Where(row => row.DeviceId == device.Id && capturedTimes.Contains(row.CapturedAt))
            .Select(row => row.CapturedAt)
            .ToListAsync(cancellationToken)).ToHashSet();

        var addedLocations = new List<LocationSample>();
        var addedBatteries = new List<BatterySample>();
        foreach (var (index, sample) in valid)
        {
            var added = false;
            if (sample.Location is { } location && locations.Add(sample.CapturedAt))
            {
                var stored = new LocationSample
                {
                    DeviceId = device.Id,
                    Latitude = location.Coordinates!.Latitude,
                    Longitude = location.Coordinates.Longitude,
                    AccuracyMeters = location.AccuracyMeters,
                    SpeedMps = location.SpeedMps,
                    CapturedAt = sample.CapturedAt,
                    ReceivedAt = now
                };
                context.LocationSamples.Add(stored);
                addedLocations.Add(stored);
                added = true;
            }
            if (sample.Battery is { } battery && batteries.Add(sample.CapturedAt))
            {
                var stored = new BatterySample
                {
                    DeviceId = device.Id,
                    BatteryLevelPercent = battery.LevelPercent,
                    IsCharging = battery.IsCharging,
                    CapturedAt = sample.CapturedAt,
                    ReceivedAt = now
                };
                context.BatterySamples.Add(stored);
                addedBatteries.Add(stored);
                added = true;
            }
            results[index] = new(added ? "accepted" : "duplicate", null);
        }
        var queued = await alerts.EvaluateAsync(device, addedLocations, addedBatteries, now, cancellationToken);
        device.LastOnlineAt = now;
        context.Entry(device).Property(row => row.LastOnlineAt).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (queued) signal.RequestProcessing();
        return SafetyResult<TelemetryBatchResultDto>.Success(new(now, results));
    }
}
