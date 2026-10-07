using FluentValidation;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(command => command.Channel).IsInEnum();
        RuleFor(command => command.Identifier).NotEmpty().MaximumLength(254);
        RuleFor(command => command.Identifier).Matches(@"\A\+[1-9][0-9]{1,14}\z")
            .When(command => command.Channel == ChallengeChannel.Sms);
        RuleFor(command => command.Identifier).EmailAddress()
            .When(command => command.Channel == ChallengeChannel.Email);
    }
}
