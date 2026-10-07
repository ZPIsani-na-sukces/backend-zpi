using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication.Login;

public sealed record LoginCommand(string Identifier, string Password, ClientType ClientType);
