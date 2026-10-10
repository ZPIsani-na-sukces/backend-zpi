using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Identity.Application.Accounts.ChangePassword;
using Zpi.Modules.Identity.Application.Accounts.CurrentAccount;
using Zpi.Modules.Identity.Application.Accounts.DeleteAccount;
using Zpi.Modules.Identity.Application.Authentication.Login;
using Zpi.Modules.Identity.Application.Authentication.Logout;
using Zpi.Modules.Identity.Application.Authentication.EmailVerification;
using Zpi.Modules.Identity.Application.Authentication.PasswordReset;
using Zpi.Modules.Identity.Application.Authentication.PhoneVerification;
using Zpi.Modules.Identity.Application.Authentication.Refresh;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Application.Accounts.UpdateProfile;

namespace Zpi.Modules.Identity.Application;

public static class IdentityApplicationSetup
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<StartPhoneVerificationHandler>();
        services.AddScoped<ConfirmPhoneVerificationHandler>();
        services.AddScoped<StartEmailVerificationHandler>();
        services.AddScoped<ConfirmEmailVerificationHandler>();
        services.AddScoped<RequestPasswordResetHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<PasswordResetCodeVerifier>();
        services.AddScoped<VerifyPasswordResetCodeHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<CurrentAccountHandler>();
        services.AddScoped<UpdateProfileHandler>();
        services.AddScoped<DeleteAccountHandler>();
        services.AddScoped<SessionRevoker>();
        services.AddScoped<AccountDeletionProcessor>();
        services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();
        return services;
    }
}
