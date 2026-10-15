namespace Zpi.Modules.Safety.Application.Incidents.Trigger;

public sealed record TriggerSosCommand(Guid AccountId, Guid SeniorId, TriggerSosRequest Request);
