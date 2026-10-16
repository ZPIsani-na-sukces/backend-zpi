using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Incidents.Acknowledge;

public sealed class AcknowledgeIncidentHandler(SafetyDbContext context, ICareFacade care, IncidentMapper mapper, IncidentReader reader, TimeProvider clock)
{
    public async Task<SafetyResult<IncidentDto>> Handle(AcknowledgeIncidentCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SafetyResult<IncidentDto>.Failed(SafetyFailure.ResourceNotFound);
        var existing = await reader.FindAsync(command.SeniorId, command.IncidentId, cancellationToken);
        if (existing is null) return SafetyResult<IncidentDto>.Failed(SafetyFailure.ResourceNotFound);
        var now = clock.GetUtcNow();
        switch (existing.Type)
        {
            case IncidentType.Sos:
                await context.SosIncidents
                    .Where(row => row.Id == command.IncidentId && row.SeniorId == command.SeniorId)
                    .Where(row => row.Status == IncidentStatus.Open)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(row => row.Status, IncidentStatus.Acknowledged)
                        .SetProperty(row => row.AcknowledgedAt, now)
                        .SetProperty(row => row.AcknowledgedByAccountId, command.AccountId)
                        .SetProperty(row => row.Version, row => row.Version + 1), cancellationToken);
                break;
            case IncidentType.GeofenceExit:
                await context.GeofenceIncidents
                    .Where(row => row.Id == command.IncidentId && row.SeniorId == command.SeniorId)
                    .Where(row => row.Status == IncidentStatus.Open)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(row => row.Status, IncidentStatus.Acknowledged)
                        .SetProperty(row => row.AcknowledgedAt, now)
                        .SetProperty(row => row.AcknowledgedByAccountId, command.AccountId)
                        .SetProperty(row => row.Version, row => row.Version + 1), cancellationToken);
                break;
            case IncidentType.LowBattery:
                await context.LowBatteryIncidents
                    .Where(row => row.Id == command.IncidentId && row.SeniorId == command.SeniorId)
                    .Where(row => row.Status == IncidentStatus.Open)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(row => row.Status, IncidentStatus.Acknowledged)
                        .SetProperty(row => row.AcknowledgedAt, now)
                        .SetProperty(row => row.AcknowledgedByAccountId, command.AccountId)
                        .SetProperty(row => row.Version, row => row.Version + 1), cancellationToken);
                break;
        }
        var row = await reader.FindAsync(command.SeniorId, command.IncidentId, cancellationToken);
        return row is null ? SafetyResult<IncidentDto>.Failed(SafetyFailure.ResourceNotFound) :
            SafetyResult<IncidentDto>.Success((await mapper.MapAsync([row], cancellationToken))[0]);
    }
}
