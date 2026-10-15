using Zpi.Modules.Notifications.Domain;

namespace Zpi.Modules.Notifications.Application.PushTokens.Register;

public sealed record RegisterPushTokenRequest(Guid InstallationId, PushPlatform Platform, string Token);
