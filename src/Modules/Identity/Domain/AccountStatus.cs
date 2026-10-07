namespace Zpi.Modules.Identity.Domain;

public enum AccountStatus
{
    PendingPhoneVerification,
    Active,
    Blocked,
    PendingDeletion
}
