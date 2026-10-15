using FluentValidation;

namespace Zpi.Modules.Notifications.Application.PushTokens.Register;

public sealed class RegisterPushTokenRequestValidator : AbstractValidator<RegisterPushTokenRequest>
{
    public RegisterPushTokenRequestValidator()
    {
        RuleFor(row => row.InstallationId).NotEmpty();
        RuleFor(row => row.Platform).IsInEnum();
        RuleFor(row => row.Token).NotEmpty().MaximumLength(4096).Must(token => token is not null && !token.Any(char.IsWhiteSpace))
            .WithMessage("Token must not contain whitespace.");
    }
}
