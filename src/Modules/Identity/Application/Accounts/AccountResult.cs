namespace Zpi.Modules.Identity.Application.Accounts;

public sealed record AccountResult<T>(T? Value, AccountFailure? Failure)
{
    public static AccountResult<T> Success(T value) => new(value, null);
    public static AccountResult<T> Failed(AccountFailure failure) => new(default, failure);
}
