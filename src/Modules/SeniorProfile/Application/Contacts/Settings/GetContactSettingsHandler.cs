using Zpi.Modules.Care.Contracts;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.Settings;

public sealed class GetContactSettingsHandler(ICareFacade care, ContactSettingsLoader loader)
{
    public async Task<SeniorProfileResult<ContactSettingsDto>> Handle(GetContactSettingsQuery query,
        CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SeniorProfileResult<ContactSettingsDto>.Failed(SeniorProfileFailure.ResourceNotFound);
        var settings = await loader.GetOrCreateAsync(query.SeniorId, cancellationToken);
        return SeniorProfileResult<ContactSettingsDto>.Success(ContactSettingsDto.From(settings));
    }
}
