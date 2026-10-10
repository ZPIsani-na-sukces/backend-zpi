using FluentValidation;

namespace Zpi.Modules.SeniorProfile.Application.Accessibility.Save;

public sealed class SaveAccessibilityPreferencesRequestValidator : AbstractValidator<SaveAccessibilityPreferencesRequest>
{
    public SaveAccessibilityPreferencesRequestValidator()
    {
        RuleFor(request => request.FontScalePercent).InclusiveBetween(100, 400);
    }
}
