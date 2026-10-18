using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Zpi.Modules.MedicalRecords.Application.Records;

namespace Zpi.Modules.MedicalRecords.Api.Forms;

internal static class MedicalRequestLimits
{
    public const int MetadataJsonBytes = 32_768;
}

public sealed class CreateMedicalRecordForm
{
    [ModelBinder(BinderType = typeof(MedicalMetadataBinder))]
    public SaveMedicalRecordMetadataRequest? Metadata { get; set; }
    public List<IFormFile> Files { get; set; } = [];
}

public sealed class AddMedicalFileForm
{
    public IFormFile? File { get; set; }
}

public sealed class MedicalContentRequest
{
    public string Disposition { get; set; } = "inline";
}
