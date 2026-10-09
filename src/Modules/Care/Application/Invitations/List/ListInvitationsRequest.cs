using Zpi.Shared.Pagination;

namespace Zpi.Modules.Care.Application.Invitations.List;

public sealed class ListInvitationsRequest : PageRequest
{
    public string Direction { get; set; } = "received";
    public string? Status { get; set; }
}
