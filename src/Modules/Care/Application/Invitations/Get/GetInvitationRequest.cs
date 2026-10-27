using Microsoft.AspNetCore.Mvc;

namespace Zpi.Modules.Care.Application.Invitations.Get;

public sealed record GetInvitationRequest([property: FromRoute(Name = "code")] string Code);
