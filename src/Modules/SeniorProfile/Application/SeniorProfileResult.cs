namespace Zpi.Modules.SeniorProfile.Application;

public sealed record SeniorProfileResult<T>(T? Value, SeniorProfileFailure? Failure)
{
    public static SeniorProfileResult<T> Success(T value) => new(value, null);
    public static SeniorProfileResult<T> Failed(SeniorProfileFailure failure) => new(default, failure);
}
