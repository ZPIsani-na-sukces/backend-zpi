using FluentValidation;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(request => request.Role).IsInEnum();
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.PhoneNumber).NotEmpty().Matches(@"\A\+[1-9][0-9]{1,14}\z");
        RuleFor(request => request.Email).EmailAddress().MaximumLength(254);
        RuleFor(request => request.Email).NotEmpty().When(request => request.Role == AccountRole.Caretaker);
        RuleFor(request => request.Password).NotNull().Length(12, 128);
    }
}
