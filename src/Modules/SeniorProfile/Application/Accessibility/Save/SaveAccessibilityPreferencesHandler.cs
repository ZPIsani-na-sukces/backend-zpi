using Zpi.Modules.Care.Contracts;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Modules.SeniorProfile.Application.Accessibility.Save;

public sealed class SaveAccessibilityPreferencesHandler(SeniorProfileDbContext context, ICareFacade care,
    AccessibilityPreferencesLoader preferences, TimeProvider clock)
{
    public async Task<SeniorProfileResult<AccessibilityPreferencesDto>> Handle(SaveAccessibilityPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SeniorProfileResult<AccessibilityPreferencesDto>.Failed(SeniorProfileFailure.ResourceNotFound);
        var preference = await preferences.GetOrCreateAsync(command.SeniorId, command.AccountId, cancellationToken);
        if (preference.Version != command.ExpectedVersion)
            return SeniorProfileResult<AccessibilityPreferencesDto>.Failed(SeniorProfileFailure.VersionMismatch);
        preference.FontScalePercent = command.FontScalePercent;
        preference.HighContrastEnabled = command.HighContrastEnabled;
        preference.LargeButtonsEnabled = command.LargeButtonsEnabled;
        preference.UpdatedAt = clock.GetUtcNow();
        preference.UpdatedByAccountId = command.AccountId;
        await context.SaveChangesAsync(cancellationToken);
        return SeniorProfileResult<AccessibilityPreferencesDto>.Success(AccessibilityPreferencesDto.From(preference));
    }
}
