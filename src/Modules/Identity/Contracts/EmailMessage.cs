namespace Zpi.Modules.Identity.Contracts;

public sealed record EmailMessage(Guid MessageId, string To, string Subject, string TextBody, string? HtmlBody = null,
    IReadOnlyList<EmailInlineImage>? InlineImages = null);
