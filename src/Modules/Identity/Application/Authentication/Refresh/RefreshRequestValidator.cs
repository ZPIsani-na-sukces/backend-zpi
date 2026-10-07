using FluentValidation;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication.Refresh;

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(request => request.ClientType).IsInEnum();
        RuleFor(request => request.RefreshToken).NotEmpty().When(request => request.ClientType == ClientType.Mobile);
        RuleFor(request => request.RefreshToken).Null().When(request => request.ClientType == ClientType.Web);
    }
}
