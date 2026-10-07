using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Identity.Application.Accounts.ChangePassword;
using Zpi.Modules.Identity.Application.Accounts.CurrentAccount;
using Zpi.Modules.Identity.Application.Accounts.DeleteAccount;
using Zpi.Modules.Identity.Application.Authentication.Login;
using Zpi.Modules.Identity.Application.Authentication.Logout;
using Zpi.Modules.Identity.Application.Authentication.Refresh;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Application.Sessions;

namespace Zpi.Modules.Identity.Application;

public static class IdentityApplicationSetup
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<CurrentAccountHandler>();
        services.AddScoped<DeleteAccountHandler>();
        services.AddScoped<SessionRevoker>();
        services.AddScoped<AccountDeletionProcessor>();
        services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();
        return services;
    }
}
