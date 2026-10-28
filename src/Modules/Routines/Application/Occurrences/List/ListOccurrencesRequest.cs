using Zpi.Modules.Routines.Domain;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Routines.Application.Occurrences.List;

public sealed class ListOccurrencesRequest : CursorRequest
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public RoutineType? Type { get; set; }
    public OccurrenceStatus? Status { get; set; }
    public Guid? TaskId { get; set; }
}
