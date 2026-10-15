namespace Zpi.Modules.Notifications.Application.PushTokens.Revoke;

public sealed record RevokePushTokenCommand(Guid AccountId, Guid SessionId, Guid RegistrationId);
