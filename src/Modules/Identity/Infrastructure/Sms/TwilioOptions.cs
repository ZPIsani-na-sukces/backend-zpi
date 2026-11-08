namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class TwilioOptions
{
    public const string SectionName = "Twilio";
    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string From { get; set; } = "";
    public string MessageTemplate { get; set; } = "Kod weryfikacyjny: {code}";

    public string AllowedNumbers { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrEmpty(AccountSid) || !string.IsNullOrEmpty(AuthToken) ||
        !string.IsNullOrEmpty(From);

    public IReadOnlySet<string> AllowedNumberSet => AllowedNumbers
        .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.Ordinal);

    public bool RestrictsRecipients => IsConfigured && AllowedNumberSet.Count > 0;
}
