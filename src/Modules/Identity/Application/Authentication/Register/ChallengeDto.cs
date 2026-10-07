namespace Zpi.Modules.Identity.Application.Authentication.Register;

public sealed record ChallengeDto(Guid ChallengeId, string Channel, DateTimeOffset ExpiresAt, DateTimeOffset ResendAfter);
