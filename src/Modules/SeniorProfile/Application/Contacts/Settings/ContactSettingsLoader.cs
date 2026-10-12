using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zpi.Modules.SeniorProfile.Domain;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.Settings;

public sealed class ContactSettingsLoader(SeniorProfileDbContext context)
{
    public async Task<ContactSettings> GetOrCreateAsync(Guid seniorId, CancellationToken cancellationToken)
    {
        var settings = await context.ContactSettings.SingleOrDefaultAsync(row => row.SeniorId == seniorId,
            cancellationToken);
        if (settings is not null) return settings;
        settings = new ContactSettings { SeniorId = seniorId };
        context.ContactSettings.Add(settings);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return settings;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.Entry(settings).State = EntityState.Detached;
            return await context.ContactSettings.SingleAsync(row => row.SeniorId == seniorId, cancellationToken);
        }
    }
}
