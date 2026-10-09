namespace Zpi.Modules.Identity.Application.Authentication;

public sealed record AuthenticationResult<T>(T? Value, AuthenticationFailure? Failure,
    PhoneVerificationDto? Verification = null, TimeSpan? RetryAfter = null)
{
    public static AuthenticationResult<T> Success(T value) => new(value, null);
    public static AuthenticationResult<T> Failed(AuthenticationFailure failure, PhoneVerificationDto? verification = null) => new(default, failure, verification);
    public static AuthenticationResult<T> CoolingDown(TimeSpan retryAfter) => new(default, AuthenticationFailure.ResendCooldown, RetryAfter: retryAfter);
}
