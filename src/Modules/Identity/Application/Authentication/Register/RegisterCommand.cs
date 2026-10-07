using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication.Register;

public sealed record RegisterCommand(AccountRole Role, string FirstName, string LastName, string PhoneNumber,
    string? Email, string Password);
