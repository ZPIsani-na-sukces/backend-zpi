using FluentValidation;

namespace Zpi.Modules.MedicalRecords.Application.Records;

public sealed class SaveMedicalRecordMetadataRequestValidator : AbstractValidator<SaveMedicalRecordMetadataRequest>
{
    public SaveMedicalRecordMetadataRequestValidator(TimeProvider clock)
    {
        RuleFor(row => row.Title).NotEmpty().MaximumLength(200);
        RuleFor(row => row.Description).MaximumLength(4000);
        RuleFor(row => row.RecordType).IsInEnum();
        RuleFor(row => row.DocumentDate).NotEmpty().Must(value => value <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
    }
}
