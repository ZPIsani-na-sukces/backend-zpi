using System.Security.Cryptography;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Zpi.Modules.MedicalRecords.Application.Files;

namespace Zpi.Modules.MedicalRecords.Infrastructure.Storage;

internal static class MedicalUploadContent
{
    public static (string Name, string MimeType) Describe(MedicalUploadFile upload, long maximum)
    {
        var name = Path.GetFileName(upload.Name.Replace('\\', '/'));
        var mime = upload.MimeType.Split(';')[0].Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl))
            throw Invalid("Invalid file name.");
        if (upload.Length <= 0) throw Invalid("The file must not be empty.");
        if (upload.Length > maximum) throw TooLarge();
        return (name, mime);
    }

    public static async Task<(long Length, string Checksum)> CopyAsync(Stream input, Stream output, string mime,
        long maximum, CancellationToken cancellationToken)
    {
        var buffer = new byte[81_920];
        var headerLength = await input.ReadAtLeastAsync(buffer.AsMemory(0, 8), 8, false, cancellationToken);
        var signatureValid = mime switch
        {
            MedicalMimeTypes.Pdf => headerLength >= 5 && buffer.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            MedicalMimeTypes.Png => headerLength >= 8 && buffer.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            MedicalMimeTypes.Jpeg => headerLength >= 3 && buffer[0] == 255 && buffer[1] == 216 && buffer[2] == 255,
            _ => false
        };
        if (!signatureValid) throw Invalid("The file must have a matching PDF, JPEG or PNG signature and content type.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long length = 0;
        var count = headerLength;
        while (count > 0)
        {
            length += count;
            if (length > maximum) throw TooLarge();
            hash.AppendData(buffer.AsSpan(0, count));
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            count = await input.ReadAsync(buffer, cancellationToken);
        }
        return (length, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    private static BadHttpRequestException TooLarge() => new("File too large.", StatusCodes.Status413PayloadTooLarge);
    private static ValidationException Invalid(string message) => new(new[] { new ValidationFailure("files", message) });
}
