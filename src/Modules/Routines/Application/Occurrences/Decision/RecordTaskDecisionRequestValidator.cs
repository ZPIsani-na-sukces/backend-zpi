using FluentValidation;

namespace Zpi.Modules.Routines.Application.Occurrences.Decision;

public sealed class RecordTaskDecisionRequestValidator : AbstractValidator<RecordTaskDecisionRequest>
{
    public RecordTaskDecisionRequestValidator(TimeProvider clock)
    {
        RuleFor(request => request.Decision).IsInEnum();
        RuleFor(request => request.PerformedAt).Must(at => at >= clock.GetUtcNow().AddDays(-30) && at <= clock.GetUtcNow().AddMinutes(5));
    }
}
