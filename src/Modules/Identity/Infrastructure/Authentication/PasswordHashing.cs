using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Infrastructure.Authentication;

public sealed class PasswordHashing
{
    private readonly PasswordHasher<Account> hasher = new(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));
    private readonly Account dummy = new()
    {
        PhoneNumber = "+10000000000",
        NormalizedPhoneNumber = "+10000000000",
        PasswordHash = "",
        FirstName = "Dummy",
        LastName = "Account"
    };
    private readonly string dummyHash;

    public PasswordHashing() => dummyHash = hasher.HashPassword(dummy, TokenService.CreateSecret());
    public string Hash(Account account, string password) => hasher.HashPassword(account, password);
    public PasswordVerificationResult Verify(Account? account, string password) =>
        hasher.VerifyHashedPassword(account ?? dummy, account?.PasswordHash ?? dummyHash, password);
}
