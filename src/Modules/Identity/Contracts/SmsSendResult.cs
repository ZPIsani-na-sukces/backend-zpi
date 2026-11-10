namespace Zpi.Modules.Identity.Contracts;

public sealed record SmsSendResult(SmsSendStatus Status, string? ProviderMessageId = null);
