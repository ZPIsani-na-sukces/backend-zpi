using Zpi.Modules.Routines.Domain;

namespace Zpi.Modules.Routines.Application.Tasks.List;

public sealed record ListRoutineTasksQuery(Guid AccountId, Guid SeniorId, RoutineType? Type, bool? IsActive,
    string? Search, int Page, int Limit);
