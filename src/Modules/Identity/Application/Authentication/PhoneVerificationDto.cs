using Zpi.Modules.Identity.Application.Authentication.Register;

namespace Zpi.Modules.Identity.Application.Authentication;

public sealed record PhoneVerificationDto(string VerificationTicket, DateTimeOffset TicketExpiresAt,
    ChallengeDto? PhoneChallenge = null);
