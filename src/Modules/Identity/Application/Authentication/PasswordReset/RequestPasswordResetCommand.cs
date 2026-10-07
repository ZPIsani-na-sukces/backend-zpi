using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed record RequestPasswordResetCommand(ChallengeChannel Channel, string Identifier);
