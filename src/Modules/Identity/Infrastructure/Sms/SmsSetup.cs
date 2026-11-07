using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public static class SmsSetup
{
    public static IServiceCollection AddIdentitySms(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TwilioOptions>().Bind(configuration.GetSection(TwilioOptions.SectionName))
            .Validate(options => !options.IsConfigured || Matches(options.AccountSid, @"\AAC[0-9a-fA-F]{32}\z"),
                "Twilio:AccountSid must be AC followed by 32 hexadecimal characters.")
            .Validate(options => !options.IsConfigured || Matches(options.AuthToken, @"\A\S{1,256}\z"),
                "Twilio:AuthToken is required and must not contain whitespace.")
            .Validate(options => !options.IsConfigured ||
                    Matches(options.From, @"\A(\+[1-9][0-9]{1,14}|(?=[A-Za-z0-9 ]*[A-Za-z])[A-Za-z0-9 ]{1,11})\z"),
                "Twilio:From must be a phone number such as +48123456789 or an alphanumeric sender ID of up to 11 characters.")
            .Validate(options => options.MessageTemplate?.Contains("{code}", StringComparison.Ordinal) == true,
                "Twilio:MessageTemplate must contain {code}.")
            .ValidateOnStart();

        services.AddHttpClient(TwilioPhoneCodeSender.ClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.MaxResponseContentBufferSize = 65_536;
        }).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        
        services.AddSingleton<TwilioPhoneCodeSender>();
        services.AddSingleton<IPhoneCodeSender>(provider =>
            provider.GetRequiredService<IOptions<TwilioOptions>>().Value.IsConfigured
                ? provider.GetRequiredService<TwilioPhoneCodeSender>()
                : provider.GetRequiredService<IHostEnvironment>().IsDevelopment()
                    ? ActivatorUtilities.CreateInstance<DevelopmentPhoneCodeSender>(provider)
                    : new UnavailablePhoneCodeSender());
        return services;
    }

    private static bool Matches(string? value, string pattern) => value is not null && Regex.IsMatch(value, pattern);
}
