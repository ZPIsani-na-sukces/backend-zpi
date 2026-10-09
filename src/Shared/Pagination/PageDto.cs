namespace Zpi.Shared.Pagination;

public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int Limit, int TotalCount);
