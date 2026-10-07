using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication;

internal static class VerificationTickets
{
    public static (VerificationTicket Ticket, string Secret) Create(IdentityDbContext context, Guid accountId, DateTimeOffset now)
    {
        var secret = TokenService.CreateSecret();
        var ticket = new VerificationTicket
        {
            AccountId = accountId,
            TokenHash = TokenService.HashSecret(secret),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30)
        };
        context.VerificationTickets.Add(ticket);
        return (ticket, secret);
    }
}
