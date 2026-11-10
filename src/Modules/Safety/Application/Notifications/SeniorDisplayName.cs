using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Safety.Application.Notifications;

internal static class SeniorDisplayName
{
    public static async Task<string> LoadAsync(IIdentityFacade identity, Guid seniorId, CancellationToken cancellationToken)
    {
        var accounts = await identity.GetAccountSummariesAsync([seniorId], cancellationToken);
        var name = accounts.TryGetValue(seniorId, out var senior) ? $"{senior.FirstName} {senior.LastName}".Trim() : "";
        return name.Length > 0 ? name : "Senior";
    }
}
