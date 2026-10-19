using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Devices.List;

public sealed class ListDevicesHandler(SafetyDbContext context, ICareFacade care)
{
    public async Task<SafetyResult<IReadOnlyList<DeviceDto>>> Handle(ListDevicesQuery query, CancellationToken cancellationToken)
    {
        if (query.SeniorId is { } seniorId && !await care.CanAccessSeniorAsync(query.AccountId, seniorId, cancellationToken))
            return SafetyResult<IReadOnlyList<DeviceDto>>.Failed(SafetyFailure.ResourceNotFound);

        var accountId = query.SeniorId ?? query.AccountId;
        var devices = context.Devices.AsNoTracking()
            .Where(row => row.AssignedAccountId == accountId && row.RetiredAt == null)
            .OrderByDescending(row => row.RegisteredAt)
            .ThenByDescending(row => row.Id);
        return SafetyResult<IReadOnlyList<DeviceDto>>.Success(await DeviceReader.ReadAsync(context, devices, cancellationToken));
    }
}
