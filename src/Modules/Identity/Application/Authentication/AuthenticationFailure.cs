namespace Zpi.Modules.Identity.Application.Authentication;

public enum AuthenticationFailure
{
    DuplicateAccount,
    InvalidCredentials,
    OperationForbidden,
    PhoneVerificationRequired,
    InvalidCode,
    InvalidState,
    DeliveryUnavailable
}
