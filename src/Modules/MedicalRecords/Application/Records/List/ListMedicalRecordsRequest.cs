using Zpi.Modules.MedicalRecords.Domain;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.MedicalRecords.Application.Records.List;

public sealed class ListMedicalRecordsRequest : CursorRequest
{
    public string? Search { get; set; }
    public MedicalRecordType? RecordType { get; set; }
    public DateOnly? DocumentDateFrom { get; set; }
    public DateOnly? DocumentDateTo { get; set; }
    public string Sort { get; set; } = MedicalRecordSort.DocumentDateDescending;
}

public static class MedicalRecordSort
{
    public const string DocumentDateDescending = "-documentDate";
    public const string DocumentDateAscending = "documentDate";
    public const string CreatedAtDescending = "-createdAt";
    public const string Title = "title";
}
