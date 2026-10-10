using Zpi.Modules.Care.Contracts;

namespace Zpi.Modules.SeniorProfile.Application.Accessibility.Get;

public sealed class GetAccessibilityPreferencesHandler(ICareFacade care, AccessibilityPreferencesLoader preferences)
{
    public async Task<SeniorProfileResult<AccessibilityPreferencesDto>> Handle(GetAccessibilityPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SeniorProfileResult<AccessibilityPreferencesDto>.Failed(SeniorProfileFailure.ResourceNotFound);
        var preference = await preferences.GetOrCreateAsync(query.SeniorId, query.AccountId, cancellationToken);
        return SeniorProfileResult<AccessibilityPreferencesDto>.Success(AccessibilityPreferencesDto.From(preference));
    }
}
