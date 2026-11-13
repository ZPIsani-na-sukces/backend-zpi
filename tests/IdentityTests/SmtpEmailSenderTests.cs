using System.Net;
using System.Text;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Email;

namespace Zpi.IdentityTests;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task Sends_arbitrary_subject_and_text_using_required_TLS_and_authentication()
    {
        var client = new RecordingSmtpClient();
        var id = Guid.CreateVersion7();
        await Sender(client).SendAsync(new EmailMessage(id, "user@example.org", "Dowolny temat", "Dowolna treść"), CancellationToken.None);
        Assert.Equal(("ssl0.ovh.net", 465, SecureSocketOptions.SslOnConnect), client.Connection);
        Assert.Equal(("no-reply@zpi-zsws.pl", "test-password"), client.Credentials);
        var message = Assert.IsType<MimeMessage>(client.Message);
        Assert.Equal("no-reply@zpi-zsws.pl", Assert.Single(message.From.Mailboxes).Address);
        Assert.Equal("user@example.org", Assert.Single(message.To.Mailboxes).Address);
        Assert.Equal("Dowolny temat", message.Subject);
        Assert.Equal("Dowolna treść", message.TextBody);
        Assert.Null(message.HtmlBody);
        Assert.Equal($"{id:N}@zpi-zsws.pl", message.MessageId);
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task Authentication_failure_is_reported_without_provider_details_or_passwords()
    {
        var client = new RecordingSmtpClient { Failure = new MailKit.Security.AuthenticationException("secret provider detail") };
        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            Sender(client).SendAsync(new EmailMessage(Guid.CreateVersion7(), "user@example.org", "Temat", "Treść"), CancellationToken.None));
        Assert.DoesNotContain("secret", exception.ToString());
        Assert.Null(client.Message);
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task Request_cancellation_is_preserved()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new RecordingSmtpClient();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Sender(client).SendAsync(new EmailMessage(Guid.CreateVersion7(), "user@example.org", "Temat", "Treść"), cancellation.Token));
        Assert.Null(client.Message);
    }

    [Theory]
    [InlineData("Email:Smtp:Password", "")]
    [InlineData("Email:Smtp:Security", "None")]
    [InlineData("Email:Smtp:Security", "StartTlsWhenAvailable")]
    [InlineData("Email:Smtp:Port", "0")]
    [InlineData("Email:Smtp:FromAddress", "invalid")]
    public void Enabled_SMTP_rejects_incomplete_or_insecure_configuration(string key, string value)
    {
        var settings = Settings();
        settings[key] = value;
        using var provider = Services(settings, Environments.Production).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);
    }

    [Theory]
    [InlineData("Development", typeof(DevelopmentEmailSender))]
    [InlineData("Production", typeof(UnavailableEmailSender))]
    public void Disabled_SMTP_preserves_the_environment_default(string environment, Type expected)
    {
        using var provider = Services(new Dictionary<string, string?>(), environment).BuildServiceProvider();
        Assert.IsType(expected, provider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void Configured_SMTP_is_used_in_development_and_production()
    {
        foreach (var environment in new[] { Environments.Development, Environments.Production })
        {
            using var provider = Services(Settings(), environment).BuildServiceProvider();
            Assert.IsType<SmtpEmailSender>(provider.GetRequiredService<IEmailSender>());
            Assert.IsType<EmailCodeSender>(provider.GetRequiredService<IEmailCodeSender>());
        }
    }

    [Fact]
    public async Task HTML_is_sent_with_a_plain_text_alternative()
    {
        var client = new RecordingSmtpClient();
        await Sender(client).SendAsync(new EmailMessage(Guid.CreateVersion7(), "user@example.org", "Raport",
            "Wynik: gotowe", "<p>Wynik: <strong>gotowe</strong></p>"), CancellationToken.None);
        var message = Assert.IsType<MimeMessage>(client.Message);
        Assert.IsType<MultipartAlternative>(message.Body);
        Assert.Equal("Wynik: gotowe", message.TextBody);
        Assert.Equal("<p>Wynik: <strong>gotowe</strong></p>", message.HtmlBody);
    }

    [Fact]
    public async Task Header_injection_is_rejected_before_connecting()
    {
        var client = new RecordingSmtpClient();
        await Assert.ThrowsAsync<ArgumentException>(() => Sender(client).SendAsync(
            new EmailMessage(Guid.CreateVersion7(), "user@example.org", "Test\r\nBcc: stranger@example.org", "Treść"), CancellationToken.None));
        Assert.Null(client.Connection);
    }

    [Fact]
    public async Task Authentication_adapter_composes_the_code_message_and_uses_the_generic_sender()
    {
        var sender = new RecordingEmailSender();
        var adapter = new EmailCodeSender(sender);
        var id = Guid.CreateVersion7();
        await adapter.SendAsync(new EmailCodeDelivery(id, "user@example.org", "012345", ChallengePurpose.EmailVerification,
            DateTimeOffset.UtcNow.AddMinutes(5)), CancellationToken.None);
        var message = Assert.Single(sender.Messages);
        Assert.Equal(id, message.MessageId);
        Assert.Equal("user@example.org", message.To);
        Assert.Equal("Potwierdź adres e-mail w Senioro", message.Subject);
        Assert.Contains("012345", message.TextBody);
        sender.IsAvailable = false;
        Assert.False(adapter.IsAvailable);
    }

    [Fact]
    public async Task Unconfigured_production_sender_reports_unavailability()
    {
        var sender = new UnavailableEmailSender();
        Assert.False(sender.IsAvailable);
        await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(
            new EmailMessage(Guid.CreateVersion7(), "user@example.org", "Temat", "Treść"), CancellationToken.None));
    }

    [Fact]
    public async Task Template_logo_is_sent_as_an_inline_CID_resource()
    {
        var transport = new RecordingSmtpClient();
        var adapter = new EmailCodeSender(Sender(transport));
        await adapter.SendAsync(new EmailCodeDelivery(Guid.CreateVersion7(), "user@example.org", "012345",
            ChallengePurpose.PasswordReset, DateTimeOffset.UtcNow.AddMinutes(5)), CancellationToken.None);
        var message = Assert.IsType<MimeMessage>(transport.Message);
        Assert.Contains("cid:senioro-logo", message.HtmlBody);
        var logo = Assert.Single(message.BodyParts, part => part.ContentId == "senioro-logo");
        Assert.Equal("image/png", logo.ContentType.MimeType);
        Assert.False(logo.IsAttachment);
        Assert.Equal("Zresetuj hasło do Senioro", message.Subject);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public bool IsAvailable { get; set; } = true;
        public List<EmailMessage> Messages { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private static SmtpEmailSender Sender(RecordingSmtpClient client) => new(Options.Create(new SmtpOptions
    {
        Enabled = true, Host = "ssl0.ovh.net", Username = "no-reply@zpi-zsws.pl", Password = "test-password",
        FromAddress = "no-reply@zpi-zsws.pl"
    }), NullLogger<SmtpEmailSender>.Instance, () => client);

    private static Dictionary<string, string?> Settings() => new()
    {
        ["Email:Smtp:Enabled"] = "true", ["Email:Smtp:Host"] = "ssl0.ovh.net", ["Email:Smtp:Port"] = "465",
        ["Email:Smtp:Username"] = "no-reply@zpi-zsws.pl", ["Email:Smtp:Password"] = "test-password",
        ["Email:Smtp:FromAddress"] = "no-reply@zpi-zsws.pl"
    };

    private static IServiceCollection Services(Dictionary<string, string?> settings, string environment) =>
        new ServiceCollection().AddLogging().AddIdentityEmail(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            new TestEnvironment { EnvironmentName = environment });

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingSmtpClient : SmtpClient
    {
        public (string Host, int Port, SecureSocketOptions Security)? Connection { get; private set; }
        public (string Username, string Password)? Credentials { get; private set; }
        public MimeMessage? Message { get; private set; }
        public Exception? Failure { get; init; }
        public bool Disposed { get; private set; }

        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Connection = (host, port, options);
            return Task.CompletedTask;
        }

        public override Task AuthenticateAsync(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken)
        {
            if (Failure is not null) throw Failure;
            var credential = credentials.GetCredential(new Uri("smtp://localhost"), "PLAIN")!;
            Credentials = (credential.UserName, credential.Password);
            return Task.CompletedTask;
        }

        public override Task<string> SendAsync(FormatOptions options, MimeMessage message, CancellationToken cancellationToken,
            ITransferProgress? progress)
        {
            Message = message;
            return Task.FromResult("accepted");
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
