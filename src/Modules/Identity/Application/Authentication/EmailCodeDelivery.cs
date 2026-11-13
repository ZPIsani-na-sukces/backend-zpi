using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication;

public sealed record EmailCodeDelivery(Guid ChallengeId, string Email, string Code, ChallengePurpose Purpose, DateTimeOffset ExpiresAt);
