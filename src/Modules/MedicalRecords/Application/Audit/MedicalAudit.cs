using Zpi.Modules.MedicalRecords.Domain;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords.Application.Audit;

public sealed class MedicalAudit(MedicalRecordsDbContext context, TimeProvider clock)
{
    public void Add(Guid actorId, Guid seniorId, Guid? recordId, Guid? fileId, string action) =>
        context.MedicalAuditEntries.Add(new MedicalAuditEntry
        {
            ActorAccountId = actorId,
            SeniorId = seniorId,
            RecordId = recordId,
            FileId = fileId,
            Action = action,
            OccurredAt = clock.GetUtcNow()
        });
}

internal static class MedicalActions
{
    public const string List = "list";
    public const string View = "view";
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string AddFile = "addFile";
    public const string DeleteFile = "deleteFile";
    public const string Download = "download";
}
