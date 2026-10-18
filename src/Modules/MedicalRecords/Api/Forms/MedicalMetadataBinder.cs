using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Zpi.Modules.MedicalRecords.Application.Records;

namespace Zpi.Modules.MedicalRecords.Api.Forms;

public sealed class MedicalMetadataBinder(IOptions<JsonOptions> options) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext context)
    {
        var form = await context.HttpContext.Request.ReadFormAsync(context.HttpContext.RequestAborted);
        var blob = form.Files.GetFile(context.FieldName);
        try
        {
            SaveMedicalRecordMetadataRequest? value;
            if (blob is not null)
            {
                if (blob.Length > MedicalRequestLimits.MetadataJsonBytes) { context.ModelState.TryAddModelError(context.ModelName, "Metadata is too large."); return; }
                await using var stream = blob.OpenReadStream();
                value = await JsonSerializer.DeserializeAsync<SaveMedicalRecordMetadataRequest>(stream, options.Value.JsonSerializerOptions, context.HttpContext.RequestAborted);
            }
            else
            {
                var raw = form[context.FieldName].ToString();
                if (Encoding.UTF8.GetByteCount(raw) > MedicalRequestLimits.MetadataJsonBytes || string.IsNullOrWhiteSpace(raw)) { context.ModelState.TryAddModelError(context.ModelName, "Provide metadata as JSON."); return; }
                value = JsonSerializer.Deserialize<SaveMedicalRecordMetadataRequest>(raw, options.Value.JsonSerializerOptions);
            }
            if (value is null) context.ModelState.TryAddModelError(context.ModelName, "Provide metadata as JSON.");
            else context.Result = ModelBindingResult.Success(value);
        }
        catch (JsonException) { context.ModelState.TryAddModelError(context.ModelName, "Invalid metadata JSON."); }
    }
}
