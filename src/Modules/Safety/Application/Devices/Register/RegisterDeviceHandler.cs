using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Devices.Register;

public sealed class RegisterDeviceHandler(SafetyDbContext context, ICareFacade care, TimeProvider clock)
{
    public async Task<SafetyResult<DeviceRegistrationDto>> Handle(RegisterDeviceCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (request.MonitoringEnabled && !await care.CanAccessSeniorAsync(command.AccountId, command.AccountId, cancellationToken))
            return SafetyResult<DeviceRegistrationDto>.Failed(SafetyFailure.OperationForbidden);

        var device = await context.Devices.SingleOrDefaultAsync(row => row.InstallationId == request.InstallationId, cancellationToken);
        if (device is not null && device.AssignedAccountId != command.AccountId)
            return SafetyResult<DeviceRegistrationDto>.Failed(SafetyFailure.ResourceNotFound);
        if (request.MonitoringEnabled && await context.Devices.AnyAsync(row => row.AssignedAccountId == command.AccountId &&
            row.MonitoringEnabled && row.RetiredAt == null && row.InstallationId != request.InstallationId, cancellationToken))
            return SafetyResult<DeviceRegistrationDto>.Failed(SafetyFailure.ActiveDeviceExists);

        var created = device is null;
        if ((created || device!.RetiredAt is not null) &&
            await context.Devices.CountAsync(row => row.AssignedAccountId == command.AccountId && row.RetiredAt == null, cancellationToken) >= 10)
            return SafetyResult<DeviceRegistrationDto>.Failed(SafetyFailure.DeviceLimitReached);

        var now = clock.GetUtcNow();
        if (device is null)
        {
            device = new Device
            {
                AssignedAccountId = command.AccountId,
                InstallationId = request.InstallationId,
                AppVersion = request.AppVersion.Trim(),
                RegisteredAt = now
            };
            context.Devices.Add(device);
        }
        else if (device.RetiredAt is not null)
        {
            device.RetiredAt = null;
            device.RegisteredAt = now;
        }
        if (device.MonitoringEnabled != request.MonitoringEnabled)
        {
            var state = await context.AutomaticAlertStates.SingleOrDefaultAsync(row => row.SeniorId == command.AccountId && row.DeviceId == device.Id, cancellationToken);
            if (state is not null) context.AutomaticAlertStates.Remove(state);
        }
        device.Platform = request.Platform;
        device.AppVersion = request.AppVersion.Trim();
        device.MonitoringEnabled = request.MonitoringEnabled;
        await context.SaveChangesAsync(cancellationToken);
        return SafetyResult<DeviceRegistrationDto>.Success(new(await DeviceReader.ReadAsync(context, device, cancellationToken), created));
    }
}
