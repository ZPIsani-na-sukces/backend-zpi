using Zpi.Modules.MedicalRecords.Application.Files;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Zpi.Modules.MedicalRecords.Application;
using Zpi.Modules.MedicalRecords.Application.Records;

namespace Zpi.Modules.MedicalRecords.Api.Forms;

public sealed class CreateMedicalRecordFormValidator : AbstractValidator<CreateMedicalRecordForm>
{
    public CreateMedicalRecordFormValidator(SaveMedicalRecordMetadataRequestValidator metadata, IOptions<MedicalRecordOptions> options)
    {
        RuleFor(row => row.Metadata).NotNull().SetValidator(metadata!);
        RuleFor(row => row.Files).NotNull().Must(files => files.Count <= options.Value.MaxFilesPerRecord);
        RuleForEach(row => row.Files).SetValidator(new MedicalFormFileValidator(options));
    }
}

public sealed class AddMedicalFileFormValidator : AbstractValidator<AddMedicalFileForm>
{
    public AddMedicalFileFormValidator(IOptions<MedicalRecordOptions> options) => RuleFor(row => row.File).NotNull().SetValidator(new MedicalFormFileValidator(options)!);
}

internal sealed class MedicalFormFileValidator : AbstractValidator<IFormFile>
{
    public MedicalFormFileValidator(IOptions<MedicalRecordOptions> options)
    {
        RuleFor(file => file.Length).GreaterThan(0).LessThanOrEqualTo(options.Value.MaxFileSizeBytes);
        RuleFor(file => file.FileName).NotEmpty().MaximumLength(255).Must(name => !name.Any(char.IsControl));
        RuleFor(file => file.ContentType).Must(type => type.Split(';')[0].Trim().ToLowerInvariant() is MedicalMimeTypes.Pdf or MedicalMimeTypes.Jpeg or MedicalMimeTypes.Png);
    }
}

public sealed class MedicalContentRequestValidator : AbstractValidator<MedicalContentRequest>
{
    public MedicalContentRequestValidator() => RuleFor(row => row.Disposition).Must(value => value is "inline" or "attachment");
}
