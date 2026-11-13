using MailKit.Security;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Infrastructure.Email;

public static class EmailSetup
{
    public static IServiceCollection AddIdentityEmail(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Host) &&
                !options.Host.Any(char.IsWhiteSpace) && !options.Host.Contains('/'), "Email:Smtp:Host must be a server hostname.")
            .Validate(options => !options.Enabled || options.Port is >= 1 and <= 65535, "Email:Smtp:Port must be between 1 and 65535.")
            .Validate(options => !options.Enabled || options.Security is SecureSocketOptions.SslOnConnect or SecureSocketOptions.StartTls,
                "Email:Smtp:Security must require TLS: SslOnConnect or StartTls.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password),
                "Email:Smtp:Username and Password are required when SMTP is enabled.")
            .Validate(options => !options.Enabled || MailboxAddress.TryParse(options.FromAddress, out var address) &&
                address.Address == options.FromAddress &&
                new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(options.FromAddress), "Email:Smtp:FromAddress must be an email address.")
            .Validate(options => !options.Enabled || options.TimeoutSeconds is >= 1 and <= 60,
                "Email:Smtp:TimeoutSeconds must be between 1 and 60.")
            .ValidateOnStart();
        services.AddSingleton<Func<ISmtpClient>>(_ => () => new SmtpClient());
        services.AddSingleton<IEmailSender>(provider =>
        {
            if (provider.GetRequiredService<IOptions<SmtpOptions>>().Value.Enabled)
                return ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider);
            return environment.IsDevelopment()
                ? ActivatorUtilities.CreateInstance<DevelopmentEmailSender>(provider)
                : new UnavailableEmailSender();
        });
        services.AddSingleton<IEmailCodeSender>(provider =>
            !provider.GetRequiredService<IOptions<SmtpOptions>>().Value.Enabled && environment.IsDevelopment()
                ? ActivatorUtilities.CreateInstance<DevelopmentEmailCodeSender>(provider)
                : ActivatorUtilities.CreateInstance<EmailCodeSender>(provider));
        return services;
    }
}
