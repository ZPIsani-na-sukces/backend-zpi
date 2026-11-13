using System.Globalization;
using System.Net;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Infrastructure.Email.Templates;

public static class AuthenticationEmailTemplate
{
    private static readonly TimeZoneInfo PolishTime = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    private static readonly byte[] Logo = LoadLogo();

    public static EmailMessage Create(EmailCodeDelivery delivery)
    {
        var (heading, intro, notice) = delivery.Purpose switch
        {
            ChallengePurpose.EmailVerification => (
                "Potwierdź adres e-mail w Senioro",
                "Aby potwierdzić swój adres e-mail, wpisz poniższy kod w aplikacji:",
                "Jeśli nie proszono Cię o potwierdzenie adresu, zignoruj tę wiadomość."),
            ChallengePurpose.PasswordReset => (
                "Zresetuj hasło do Senioro",
                "Otrzymaliśmy prośbę o zresetowanie hasła do Twojego konta. Wpisz poniższy kod w aplikacji:",
                "Jeśli to nie Ty wysłałeś tę prośbę, zignoruj wiadomość. Twoje hasło pozostanie bez zmian."),
            _ => throw new ArgumentOutOfRangeException(nameof(delivery), "Unsupported email purpose.")
        };
        var expires = TimeZoneInfo.ConvertTime(delivery.ExpiresAt, PolishTime).ToString("dd.MM.yyyy 'o' HH:mm:ss", CultureInfo.InvariantCulture);
        var expiry = $"Kod jest ważny do {expires} (czas polski).";
        var text = $"{heading}\n\nCześć!\n\n{intro}\n\n{delivery.Code}\n\n{expiry}\nNie udostępniaj tego kodu innym osobom.\n\n{notice}\n\nZespół Senioro";
        var html = $$"""
            <!doctype html>
            <html lang="pl">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>{{Encode(heading)}}</title></head>
            <body style="margin:0;padding:0;background-color:#ffffff;color:#262626;font-family:Arial,Helvetica,sans-serif;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;">{{Encode(heading)}}. {{Encode(expiry)}}</div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#ffffff;">
                <tr><td align="center" style="padding:32px 20px;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;">
                    <tr><td style="padding:0 0 28px;border-bottom:1px solid #e7e7e7;">
                      <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                        <td width="36" valign="middle"><img src="cid:senioro-logo" width="23" height="32" alt="Logo Senioro" style="display:block;border:0;"></td>
                        <td valign="middle" style="font-size:18px;line-height:26px;font-weight:600;color:#333333;">Senioro</td>
                      </tr></table>
                    </td></tr>
                    <tr><td style="padding:28px 0 0;">
                      <h1 style="margin:0 0 28px;font-size:22px;line-height:30px;font-weight:600;color:#222222;">{{Encode(heading)}}</h1>
                      <p style="margin:0 0 16px;font-size:16px;line-height:26px;">Cześć!</p>
                      <p style="margin:0 0 24px;font-size:16px;line-height:26px;">{{Encode(intro)}}</p>
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="table-layout:fixed;">
                        <tr><td style="padding:20px;background-color:#f6f6f6;border:1px solid #e7e7e7;">
                          <p style="margin:0 0 10px;font-size:13px;line-height:20px;color:#666666;">Twój kod</p>
                          <div dir="ltr" style="font-family:Consolas,'Courier New',monospace;font-size:24px;line-height:36px;font-weight:600;color:#222222;word-break:break-all;overflow-wrap:anywhere;">{{Encode(delivery.Code)}}</div>
                        </td></tr>
                      </table>
                      <p style="margin:20px 0 6px;font-size:14px;line-height:23px;color:#555555;">Kod jest ważny do <strong style="font-weight:700;color:#262626;">{{Encode(expires)}}</strong> (czas polski).</p>
                      <p style="margin:0 0 28px;font-size:14px;line-height:23px;color:#555555;">Nie udostępniaj tego kodu innym osobom.</p>
                      <p style="margin:0;font-size:14px;line-height:23px;color:#555555;">{{Encode(notice)}}</p>
                      <p align="right" style="margin:32px 0 0;text-align:right;font-size:15px;line-height:25px;color:#333333;">Zespół Senioro</p>
                    </td></tr>
                    <tr><td style="padding:24px 0 0;font-size:12px;line-height:20px;color:#777777;">Wiadomość została wysłana automatycznie. Nie odpowiadaj na nią.</td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
        return new EmailMessage(delivery.ChallengeId, delivery.Email, heading, text, html,
            [new EmailInlineImage("senioro-logo", "image/png", Logo)]);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static byte[] LoadLogo()
    {
        using var stream = typeof(AuthenticationEmailTemplate).Assembly.GetManifestResourceStream(
            "Zpi.Modules.Identity.Infrastructure.Email.Templates.senioro-logo.png")!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
