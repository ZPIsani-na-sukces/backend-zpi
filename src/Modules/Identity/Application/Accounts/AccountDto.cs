using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Accounts;

public sealed record AccountDto(Guid UserId, string Role, string FirstName, string LastName, string? Email,
    string PhoneNumber, bool PhoneVerified, bool EmailVerified, string Status, int Version);
