using Zpi.Domain;

namespace Zpi.Modules.Identity.Domain;

public sealed class VerificationChallenge : VersionedEntity
{
    public const int MaximumAttempts = 5;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? AccountId { get; set; }
    public Guid? TicketId { get; set; }
    public ChallengePurpose Purpose { get; set; }
    public ChallengeChannel Channel { get; set; }
    public required string Destination { get; set; }
    public required string SecretHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset ResendAfter { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? InvalidatedAt { get; set; }
}
