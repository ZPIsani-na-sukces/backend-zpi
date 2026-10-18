using FluentValidation;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.MedicalRecords.Application.Records.List;

public sealed class ListMedicalRecordsRequestValidator : PageRequestValidator<ListMedicalRecordsRequest>
{
    public ListMedicalRecordsRequestValidator()
    {
        RuleFor(row => row.Search).MaximumLength(100);
        RuleFor(row => row.RecordType).IsInEnum().When(row => row.RecordType.HasValue);
        RuleFor(row => row.DocumentDateTo).Must((row, end) => !row.DocumentDateFrom.HasValue || !end.HasValue || end >= row.DocumentDateFrom);
        RuleFor(row => row.Sort).Must(value => value is MedicalRecordSort.DocumentDateDescending or MedicalRecordSort.DocumentDateAscending
            or MedicalRecordSort.CreatedAtDescending or MedicalRecordSort.Title);
    }
}
