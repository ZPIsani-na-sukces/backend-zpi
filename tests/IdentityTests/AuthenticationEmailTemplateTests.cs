using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Email.Templates;

namespace Zpi.IdentityTests;

public sealed class AuthenticationEmailTemplateTests
{
    [Theory]
    [InlineData(ChallengePurpose.EmailVerification, "Potwierdź adres e-mail w Senioro", "Jeśli nie proszono Cię o potwierdzenie adresu")]
    [InlineData(ChallengePurpose.PasswordReset, "Zresetuj hasło do Senioro", "Twoje hasło pozostanie bez zmian")]
    public void Purpose_has_its_own_subject_instructions_and_both_body_formats(ChallengePurpose purpose, string subject, string notice)
    {
        var id = Guid.CreateVersion7();
        var message = AuthenticationEmailTemplate.Create(new EmailCodeDelivery(id, "user@example.org", "token-value",
            purpose, new DateTimeOffset(2026, 11, 11, 21, 15, 0, TimeSpan.Zero)));
        Assert.Equal(id, message.MessageId);
        Assert.Equal(subject, message.Subject);
        Assert.Contains(subject, message.TextBody);
        Assert.Contains(notice, message.TextBody);
        Assert.Contains("token-value", message.TextBody);
        Assert.Contains("token-value", message.HtmlBody);
        Assert.Contains("11.11.2026 o 22:15:00 (czas polski)", message.TextBody);
        Assert.Contains("cid:senioro-logo", message.HtmlBody);
        Assert.Equal("image/png", Assert.Single(message.InlineImages!).ContentType);
        Assert.NotEmpty(Assert.Single(message.InlineImages!).Content);
        if (purpose == ChallengePurpose.EmailVerification) Assert.DoesNotContain("zresetowanie hasła", message.TextBody);
        else Assert.DoesNotContain("potwierdzić swój adres", message.TextBody);
    }

    [Fact]
    public void HTML_encodes_the_complete_token_without_altering_the_plain_text_version()
    {
        const string code = "<script>&\"'token";
        var message = AuthenticationEmailTemplate.Create(new EmailCodeDelivery(Guid.CreateVersion7(), "user@example.org", code,
            ChallengePurpose.EmailVerification, DateTimeOffset.UtcNow.AddMinutes(5)));
        Assert.Contains(code, message.TextBody);
        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;&amp;&quot;", message.HtmlBody);
    }

    [Fact]
    public void Expiry_uses_the_supplied_deadline_and_accounts_for_daylight_saving_time()
    {
        var message = AuthenticationEmailTemplate.Create(new EmailCodeDelivery(Guid.CreateVersion7(), "user@example.org", "token",
            ChallengePurpose.PasswordReset, new DateTimeOffset(2026, 7, 1, 12, 34, 56, TimeSpan.Zero)));
        Assert.Contains("01.07.2026 o 14:34:56 (czas polski)", message.TextBody);
    }

    [Fact]
    public void Unsupported_purposes_do_not_generate_a_misleading_email()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthenticationEmailTemplate.Create(new EmailCodeDelivery(
            Guid.CreateVersion7(), "user@example.org", "token", ChallengePurpose.PhoneVerification, DateTimeOffset.UtcNow)));
    }
}
