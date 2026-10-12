using Zpi.Shared.Pagination;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.List;

public sealed class ListContactsRequest : PageRequest
{
    public string? Search { get; set; }
    public bool IsActive { get; set; } = true;
    public string Sort { get; set; } = "displayName";
}
