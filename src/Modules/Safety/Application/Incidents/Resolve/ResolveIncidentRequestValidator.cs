using FluentValidation;

namespace Zpi.Modules.Safety.Application.Incidents.Resolve;

public sealed class ResolveIncidentRequestValidator : AbstractValidator<ResolveIncidentRequest>
{
    public ResolveIncidentRequestValidator() => RuleFor(row => row.Note).MaximumLength(1000);
}
