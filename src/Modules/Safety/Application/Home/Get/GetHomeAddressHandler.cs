using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Home.Get;

public sealed class GetHomeAddressHandler(SafetyDbContext context, ICareFacade care)
{
    public async Task<SafetyResult<HomeAddressDto>> Handle(GetHomeAddressQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SafetyResult<HomeAddressDto>.Failed(SafetyFailure.ResourceNotFound);
        var row = await context.HomeAddresses
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.SeniorId == query.SeniorId && row.DeletedAt == null, cancellationToken);
        return row is null ? SafetyResult<HomeAddressDto>.Failed(SafetyFailure.ResourceNotFound) : SafetyResult<HomeAddressDto>.Success(HomeAddressDto.From(row));
    }
}
