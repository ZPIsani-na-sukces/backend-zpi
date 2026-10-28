using Zpi.Modules.Safety.Domain;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Safety.Application.Incidents.List;

public sealed class ListIncidentsRequest : CursorRequest
{
    public IncidentType? Type { get; init; }
    public IncidentStatus? Status { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}
