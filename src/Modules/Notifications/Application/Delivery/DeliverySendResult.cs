namespace Zpi.Modules.Notifications.Application.Delivery;

public sealed record DeliverySendResult(bool Accepted, string? ProviderMessageId = null, string? ErrorCode = null,
    bool Retryable = true, bool InvalidToken = false);
