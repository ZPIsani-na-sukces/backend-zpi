namespace Zpi.Modules.Routines.Application;

public sealed record RoutineResult<T>(T? Value, RoutineFailure? Failure)
{
    public static RoutineResult<T> Success(T value) => new(value, null);
    public static RoutineResult<T> Failed(RoutineFailure failure) => new(default, failure);
}
