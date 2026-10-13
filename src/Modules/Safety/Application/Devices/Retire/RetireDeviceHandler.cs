using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Devices.Retire;

public sealed class RetireDeviceHandler(SafetyDbContext context, TimeProvider clock)
{
    public async Task<SafetyResult<bool>> Handle(RetireDeviceCommand command, CancellationToken cancellationToken)
    {
        var device = await context.Devices.SingleOrDefaultAsync(row => row.Id == command.DeviceId &&
            row.AssignedAccountId == command.AccountId, cancellationToken);
        if (device is null) return SafetyResult<bool>.Failed(SafetyFailure.ResourceNotFound);
        if (device.RetiredAt is null)
        {
            device.MonitoringEnabled = false;
            device.RetiredAt = clock.GetUtcNow();
            await context.SaveChangesAsync(cancellationToken);
        }
        return SafetyResult<bool>.Success(true);
    }
}
