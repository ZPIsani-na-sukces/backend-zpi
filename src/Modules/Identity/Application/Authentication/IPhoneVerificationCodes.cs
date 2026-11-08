namespace Zpi.Modules.Identity.Application.Authentication;

public interface IPhoneVerificationCodes
{
    string? FixedCodeFor(string phoneNumber);
}
