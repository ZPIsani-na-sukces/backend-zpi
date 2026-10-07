using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication.Refresh;

public sealed record RefreshRequest(ClientType ClientType, string? RefreshToken = null);
