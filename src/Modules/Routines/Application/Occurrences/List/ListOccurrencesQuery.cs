using Zpi.Modules.Routines.Domain;

namespace Zpi.Modules.Routines.Application.Occurrences.List;

public sealed record ListOccurrencesQuery(Guid AccountId, Guid SeniorId, DateTimeOffset From, DateTimeOffset To,
    RoutineType? Type, OccurrenceStatus? Status, Guid? TaskId, string? Cursor, int Limit);
