using System.Threading.Channels;
using Zpi.Modules.Notifications.Contracts;

namespace Zpi.Modules.Notifications.Infrastructure.BackgroundJobs;

internal sealed class NotificationProcessingSignal : INotificationProcessingSignal
{
    private readonly Channel<bool> requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public void RequestProcessing() => requests.Writer.TryWrite(true);

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try { await requests.Reader.ReadAsync(timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
    }
}
