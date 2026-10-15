namespace Zpi.Modules.Notifications.Application.PushTokens;

public sealed record PushTokenDto(Guid RegistrationId, Guid InstallationId, string Platform, DateTimeOffset RegisteredAt, DateTimeOffset UpdatedAt);
