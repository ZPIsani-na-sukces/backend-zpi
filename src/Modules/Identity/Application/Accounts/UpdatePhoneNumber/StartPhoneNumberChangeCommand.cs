namespace Zpi.Modules.Identity.Application.Accounts.UpdatePhoneNumber;

public sealed record StartPhoneNumberChangeCommand(Guid AccountId, string PhoneNumber);
