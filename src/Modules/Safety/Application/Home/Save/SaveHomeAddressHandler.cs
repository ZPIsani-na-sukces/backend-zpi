using System.Data;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Home.Save;

public sealed class SaveHomeAddressHandler(SafetyDbContext context, ICareFacade care, TimeProvider clock)
{
    public async Task<SafetyResult<HomeAddressDto>> Handle(SaveHomeAddressCommand command, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SafetyResult<HomeAddressDto>.Failed(SafetyFailure.ResourceNotFound);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var row = await context.HomeAddresses.SingleOrDefaultAsync(row => row.SeniorId == command.SeniorId, cancellationToken);
        if (row is not null && row.Version != command.ExpectedVersion || row is null && command.ExpectedVersion.HasValue)
            return SafetyResult<HomeAddressDto>.Failed(SafetyFailure.VersionMismatch);
        var request = command.Request;
        var now = clock.GetUtcNow();
        if (row is null)
        {
            row = new HomeAddress
            {
                SeniorId = command.SeniorId,
                CountryCode = request.CountryCode,
                PostalCode = request.PostalCode.Trim(),
                City = request.City.Trim(),
                BuildingNumber = request.BuildingNumber.Trim(),
                CreatedAt = now,
                CreatedByAccountId = command.AccountId
            };
            context.HomeAddresses.Add(row);
        }
        row.CountryCode = request.CountryCode;
        row.PostalCode = request.PostalCode.Trim();
        row.City = request.City.Trim();
        row.Street = string.IsNullOrWhiteSpace(request.Street) ? null : request.Street.Trim();
        row.BuildingNumber = request.BuildingNumber.Trim();
        row.ApartmentNumber = string.IsNullOrWhiteSpace(request.ApartmentNumber) ? null : request.ApartmentNumber.Trim();
        row.Latitude = request.Coordinates.Latitude;
        row.Longitude = request.Coordinates.Longitude;
        row.UpdatedAt = now;
        row.UpdatedByAccountId = command.AccountId;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SafetyResult<HomeAddressDto>.Success(HomeAddressDto.From(row));
    }
}
