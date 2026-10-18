namespace Zpi.Modules.MedicalRecords.Application;

public sealed record MedicalResult<T>(T? Value, MedicalFailure? Failure)
{
    public static MedicalResult<T> Success(T value) => new(value, null);
    public static MedicalResult<T> Failed(MedicalFailure failure) => new(default, failure);
}
