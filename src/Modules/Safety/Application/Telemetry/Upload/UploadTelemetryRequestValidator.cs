using FluentValidation;

namespace Zpi.Modules.Safety.Application.Telemetry.Upload;

public sealed class UploadTelemetryRequestValidator : AbstractValidator<UploadTelemetryRequest>
{
    public UploadTelemetryRequestValidator()
    {
        RuleFor(request => request.Samples).NotEmpty().Must(samples => samples is null || samples.Count <= 500);
    }
}
