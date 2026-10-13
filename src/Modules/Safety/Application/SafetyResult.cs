namespace Zpi.Modules.Safety.Application;

public sealed record SafetyResult<T>(T? Value, SafetyFailure? Failure)
{
    public static SafetyResult<T> Success(T value) => new(value, null);
    public static SafetyResult<T> Failed(SafetyFailure failure) => new(default, failure);
}
