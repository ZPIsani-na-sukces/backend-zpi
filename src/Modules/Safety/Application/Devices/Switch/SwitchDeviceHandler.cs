using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Devices.Switch;

public sealed class SwitchDeviceHandler(SafetyDbContext context, ICareFacade care)
{
    public async Task<SafetyResult<DeviceDto>> Handle(SwitchDeviceCommand command, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(command.AccountId, command.AccountId, cancellationToken))
            return SafetyResult<DeviceDto>.Failed(SafetyFailure.OperationForbidden);
        var device = await context.Devices.SingleOrDefaultAsync(row => row.Id == command.DeviceId &&
            row.AssignedAccountId == command.AccountId && row.RetiredAt == null, cancellationToken);
        if (device is null) return SafetyResult<DeviceDto>.Failed(SafetyFailure.ResourceNotFound);
        if (device.MonitoringEnabled) return SafetyResult<DeviceDto>.Success(await DeviceReader.ReadAsync(context, device, cancellationToken));

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var previous = await context.Devices.SingleOrDefaultAsync(row => row.AssignedAccountId == command.AccountId &&
            row.MonitoringEnabled && row.RetiredAt == null, cancellationToken);
        if (previous is not null)
        {
            previous.MonitoringEnabled = false;
            await context.SaveChangesAsync(cancellationToken);
        }
        var state = await context.AutomaticAlertStates.SingleOrDefaultAsync(row => row.SeniorId == command.AccountId, cancellationToken);
        if (state is not null) context.AutomaticAlertStates.Remove(state);
        device.MonitoringEnabled = true;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SafetyResult<DeviceDto>.Success(await DeviceReader.ReadAsync(context, device, cancellationToken));
    }
}
