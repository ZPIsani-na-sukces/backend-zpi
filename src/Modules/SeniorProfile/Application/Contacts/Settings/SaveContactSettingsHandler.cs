using Zpi.Modules.Care.Contracts;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.Settings;

public sealed class SaveContactSettingsHandler(ICareFacade care, ContactSettingsLoader loader,
    SeniorProfileDbContext context)
{
    public async Task<SeniorProfileResult<ContactSettingsDto>> Handle(SaveContactSettingsCommand command,
        CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SeniorProfileResult<ContactSettingsDto>.Failed(SeniorProfileFailure.ResourceNotFound);
        var settings = await loader.GetOrCreateAsync(command.SeniorId, cancellationToken);
        if (settings.Version != command.ExpectedVersion)
            return SeniorProfileResult<ContactSettingsDto>.Failed(SeniorProfileFailure.VersionMismatch);
        settings.SeniorEditingLocked = command.SeniorEditingLocked;
        await context.SaveChangesAsync(cancellationToken);
        return SeniorProfileResult<ContactSettingsDto>.Success(ContactSettingsDto.From(settings));
    }
}
