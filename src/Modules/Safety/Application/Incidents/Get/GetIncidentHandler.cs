using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Incidents.Get;

public sealed class GetIncidentHandler(IncidentReader reader, ICareFacade care, IncidentMapper mapper)
{
    public async Task<SafetyResult<IncidentDto>> Handle(GetIncidentQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SafetyResult<IncidentDto>.Failed(SafetyFailure.ResourceNotFound);
        var row = await reader.FindAsync(query.SeniorId, query.IncidentId, cancellationToken);
        return row is null ? SafetyResult<IncidentDto>.Failed(SafetyFailure.ResourceNotFound) :
            SafetyResult<IncidentDto>.Success((await mapper.MapAsync([row], cancellationToken))[0]);
    }
}
