using Zpi.Shared.Pagination;

namespace Zpi.Modules.Care.Application.Relationships.ListSeniors;

public sealed class ListSeniorsRequest : CursorRequest
{
    public string? Search { get; set; }
}
