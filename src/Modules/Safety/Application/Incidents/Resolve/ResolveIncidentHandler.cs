using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Incidents.Resolve;

public sealed class ResolveIncidentHandler(SafetyDbContext context, ICareFacade care, IncidentMapper mapper, TimeProvider clock)
{
    public async Task<SafetyResult<IncidentDto>> Handle(ResolveIncidentCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SafetyResult<IncidentDto>.Failed(SafetyFailure.ResourceNotFound);
        var rows = context.SosIncidents
            .Where(row => row.Id == command.IncidentId && row.SeniorId == command.SeniorId);
        var now = clock.GetUtcNow();
        var note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim();
        await rows
            .Where(row => row.Status != IncidentStatus.Resolved).ExecuteUpdateAsync(update => update
            .SetProperty(row => row.Status, IncidentStatus.Resolved)
            .SetProperty(row => row.ResolvedAt, now)
            .SetProperty(row => row.ResolvedByAccountId, command.AccountId)
            .SetProperty(row => row.ResolutionNote, note)
            .SetProperty(row => row.Version, row => row.Version + 1), cancellationToken);
        var row = await rows
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? SafetyResult<IncidentDto>.Failed(SafetyFailure.ResourceNotFound) :
            SafetyResult<IncidentDto>.Success((await mapper.MapAsync([row], cancellationToken))[0]);
    }
}
