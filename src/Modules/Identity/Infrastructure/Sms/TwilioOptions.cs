namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class TwilioOptions
{
    public const string SectionName = "Twilio";
    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string From { get; set; } = "";
    public string MessageTemplate { get; set; } = "Kod weryfikacyjny: {code}";

    public bool IsConfigured => !string.IsNullOrEmpty(AccountSid) || !string.IsNullOrEmpty(AuthToken) ||
        !string.IsNullOrEmpty(From);
}
