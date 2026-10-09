using Zpi.Shared.Pagination;

namespace Zpi.Modules.Care.Application.Relationships.ListSeniors;

public sealed class ListSeniorsRequest : PageRequest
{
    public string? Search { get; set; }
}
