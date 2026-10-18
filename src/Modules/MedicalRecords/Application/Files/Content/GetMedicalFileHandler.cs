using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.MedicalRecords.Application.Audit;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords.Application.Files.Content;

public sealed class GetMedicalFileHandler(MedicalRecordsDbContext context, ICareFacade care, IMedicalFileStorage storage, MedicalAudit audit)
{
    public async Task<MedicalResult<MedicalFileContent>> Handle(GetMedicalFileQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return MedicalResult<MedicalFileContent>.Failed(MedicalFailure.ResourceNotFound);
        if (!await context.MedicalRecords.AnyAsync(row => row.Id == query.RecordId && row.SeniorId == query.SeniorId && row.DeletedAt == null, cancellationToken))
            return MedicalResult<MedicalFileContent>.Failed(MedicalFailure.ResourceNotFound);
        var file = await context.MedicalFiles
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == query.FileId &&
            row.MedicalRecordId == query.RecordId && row.DeletedAt == null, cancellationToken);
        if (file is null) return MedicalResult<MedicalFileContent>.Failed(MedicalFailure.ResourceNotFound);
        var stream = await storage.OpenReadAsync(file.ObjectKey, cancellationToken);
        if (stream is null) return MedicalResult<MedicalFileContent>.Failed(MedicalFailure.ResourceNotFound);
        try
        {
            var checksum = await SHA256.HashDataAsync(stream, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(checksum, Convert.FromHexString(file.ChecksumSha256)))
                throw new MedicalStorageException(new IOException("Stored medical file checksum does not match."));
            stream.Position = 0;
            audit.Add(query.AccountId, query.SeniorId, query.RecordId, file.Id, MedicalActions.Download);
            await context.SaveChangesAsync(cancellationToken);
            return MedicalResult<MedicalFileContent>.Success(new(stream, file.MimeType, file.OriginalName));
        }
        catch { await stream.DisposeAsync(); throw; }
    }
}
