using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication.Register;

public sealed record RegistrationDto(Guid UserId, string Status, string VerificationTicket,
    DateTimeOffset TicketExpiresAt, ChallengeDto PhoneChallenge);
