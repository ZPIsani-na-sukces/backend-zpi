namespace Zpi.Modules.Notifications.Application.PushTokens.Register;

public sealed record RegisterPushTokenCommand(Guid AccountId, Guid SessionId, RegisterPushTokenRequest Request);
