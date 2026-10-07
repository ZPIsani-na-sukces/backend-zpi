using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication;

public sealed record PhoneVerificationDto(string VerificationTicket, DateTimeOffset TicketExpiresAt);
