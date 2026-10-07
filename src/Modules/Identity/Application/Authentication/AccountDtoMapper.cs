using Zpi.Domain;
using Zpi.Modules.Identity.Application.Accounts;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication;

internal static class AccountDtoMapper
{
    public static AccountDto Map(Account account, AccountRole role) => new(account.Id,
        role.ToCamelCase(), account.FirstName, account.LastName, account.Email,
        account.PhoneNumber, account.PhoneVerifiedAt is not null, account.EmailVerifiedAt is not null,
        account.Status.ToCamelCase(), account.Version);
}
