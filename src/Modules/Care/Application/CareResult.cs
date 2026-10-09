namespace Zpi.Modules.Care.Application;

public sealed record CareResult<T>(T? Value, CareFailure? Failure)
{
    public static CareResult<T> Success(T value) => new(value, null);
    public static CareResult<T> Failed(CareFailure failure) => new(default, failure);
}
