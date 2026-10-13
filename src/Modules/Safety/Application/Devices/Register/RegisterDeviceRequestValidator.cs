using FluentValidation;

namespace Zpi.Modules.Safety.Application.Devices.Register;

public sealed class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(request => request.InstallationId).NotEmpty();
        RuleFor(request => request.Platform).IsInEnum();
        RuleFor(request => request.AppVersion).NotEmpty().MaximumLength(100);
    }
}
