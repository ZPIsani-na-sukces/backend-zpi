using Zpi.Modules.Routines.Domain;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Routines.Application.Tasks.List;

public sealed class ListRoutineTasksRequest : CursorRequest
{
    public RoutineType? Type { get; set; }
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
}
