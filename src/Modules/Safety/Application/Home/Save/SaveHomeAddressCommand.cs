namespace Zpi.Modules.Safety.Application.Home.Save;

public sealed record SaveHomeAddressCommand(Guid AccountId, Guid SeniorId, SaveHomeAddressRequest Request, int? ExpectedVersion);
