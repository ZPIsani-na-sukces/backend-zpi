using System.Globalization;
using System.Security.Cryptography;
using Zpi.Modules.Care.Application.Invitations;

namespace Zpi.Modules.Care.Infrastructure;

internal sealed class InvitationCodeGenerator : IInvitationCodeGenerator
{
    public string Generate() => RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
}
