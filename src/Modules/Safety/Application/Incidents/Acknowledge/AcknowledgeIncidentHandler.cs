using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Incidents.Acknowledge;

public sealed class AcknowledgeIncidentHandler(SafetyDbContext context, ICareFacade care, IncidentMapper mapper, TimeProvider clock)
{
    public async Task<SafetyResult<IncidentDto>> Handle(AcknowledgeIncidentCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SafetyResult<IncidentDto>.Failed(SafetyFailure.ResourceNotFound);
        var rows = context.SosIncidents
            .Where(row => row.Id == command.IncidentId && row.SeniorId == command.SeniorId);
        var now = clock.GetUtcNow();
        await rows
            .Where(row => row.Status == IncidentStatus.Open).ExecuteUpdateAsync(update => update
            .SetProperty(row => row.Status, IncidentStatus.Acknowledged)
            .SetProperty(row => row.AcknowledgedAt, now)
            .SetProperty(row => row.AcknowledgedByAccountId, command.AccountId)
            .SetProperty(row => row.Version, row => row.Version + 1), cancellationToken);
        var row = await rows
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? SafetyResult<IncidentDto>.Failed(SafetyFailure.ResourceNotFound) :
            SafetyResult<IncidentDto>.Success((await mapper.MapAsync([row], cancellationToken))[0]);
    }
}
