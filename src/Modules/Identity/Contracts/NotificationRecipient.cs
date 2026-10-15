namespace Zpi.Modules.Identity.Contracts;

public sealed record NotificationRecipient(Guid UserId, string Role, string PhoneNumber);
