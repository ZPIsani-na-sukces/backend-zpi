using System.Net.Sockets;
using System.Security.Authentication;
using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Identity.Infrastructure.Email;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger, Func<ISmtpClient> createClient) : IEmailSender
{
    public bool IsAvailable => options.Value.Enabled;

    public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        if (!IsAvailable) throw new EmailDeliveryException();
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.To);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.TextBody);
        if (email.Subject.Contains('\r') || email.Subject.Contains('\n'))
            throw new ArgumentException("Email subject must not contain line breaks.", nameof(email));
        var settings = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.MessageId = $"{email.MessageId:N}@{settings.FromAddress.Split('@')[1]}";
        var body = new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody };
        foreach (var image in email.InlineImages ?? [])
        {
            var resource = body.LinkedResources.Add(image.ContentId, image.Content, ContentType.Parse(image.ContentType));
            resource.ContentId = image.ContentId;
        }
        message.Body = body.ToMessageBody();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var client = createClient();
        client.Timeout = settings.TimeoutSeconds * 1000;
        try
        {
            await client.ConnectAsync(settings.Host, settings.Port, settings.Security, timeout.Token);
            await client.AuthenticateAsync(settings.Username, settings.Password, timeout.Token);
            await client.SendAsync(message, timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is IOException or SocketException or SmtpCommandException or
            SmtpProtocolException or MailKit.Security.AuthenticationException or AuthenticationException or
            ServiceNotAuthenticatedException or ServiceNotConnectedException or OperationCanceledException)
        {
            logger.LogWarning("SMTP delivery failed for message {MessageId}: {FailureType}.", email.MessageId, exception.GetType().Name);
            throw new EmailDeliveryException();
        }
    }
}
