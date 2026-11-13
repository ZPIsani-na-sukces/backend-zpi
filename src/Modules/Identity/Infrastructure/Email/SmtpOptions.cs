using MailKit.Security;

namespace Zpi.Modules.Identity.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 465;
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.SslOnConnect;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Senioro";
    public int TimeoutSeconds { get; set; } = 15;
}
