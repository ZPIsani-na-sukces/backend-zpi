namespace Zpi.Shared.Pagination;

public sealed record CursorPageDto<T>(IReadOnlyList<T> Items, string? NextCursor);
