namespace Zpi.Modules.Identity.Application.Accounts.DeleteAccount;

public sealed class AccountDeletionOptions
{
    public const string SectionName = "AccountDeletion";
    public static readonly TimeSpan MaximumGracePeriod = TimeSpan.FromDays(365);

    // Time between a deletion request and removal of the account and its data. Zero deletes it during the request.
    public TimeSpan GracePeriod { get; set; } = TimeSpan.FromDays(30);
}
