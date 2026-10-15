namespace Zpi.Modules.Notifications.Application;

public sealed record NotificationResult<T>(T? Value, NotificationFailure? Failure)
{
    public static NotificationResult<T> Success(T value) => new(value, null);
    public static NotificationResult<T> Failed(NotificationFailure failure) => new(default, failure);
}
public enum NotificationFailure { ResourceNotFound, TokenAlreadyRegistered, TokenLimitReached }
