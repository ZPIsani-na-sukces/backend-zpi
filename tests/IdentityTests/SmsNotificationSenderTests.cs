using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Notifications.Application.Delivery;
using Zpi.Modules.Notifications.Infrastructure.Delivery;

namespace Zpi.IdentityTests;

public sealed class SmsNotificationSenderTests
{
    [Fact]
    public async Task Sends_the_notification_text_and_keeps_the_provider_message_id()
    {
        var sms = new FakeSms(new SmsSendResult(SmsSendStatus.Sent, "SM0123"));

        var result = await new SmsNotificationSender(sms).SendAsync("abc", "+48123456789", "SOS: Jan Kowalski potrzebuje pomocy.",
            CancellationToken.None);

        Assert.Equal(new DeliverySendResult(true, "SM0123"), result);
        Assert.Equal(("notification delivery abc", "+48123456789", "SOS: Jan Kowalski potrzebuje pomocy."), Assert.Single(sms.Sent));
    }

    [Theory]
    [InlineData(SmsSendStatus.InvalidPhoneNumber, "invalidPhoneNumber", false)]
    [InlineData(SmsSendStatus.Unavailable, "providerUnavailable", true)]
    public async Task Failures_tell_the_worker_whether_to_retry(SmsSendStatus status, string errorCode, bool retryable)
    {
        var result = await new SmsNotificationSender(new FakeSms(new SmsSendResult(status)))
            .SendAsync("abc", "+48123456789", "Test", CancellationToken.None);

        Assert.Equal(new DeliverySendResult(false, ErrorCode: errorCode, Retryable: retryable), result);
    }

    [Fact]
    public async Task Without_a_configured_provider_nothing_is_sent_or_retried()
    {
        var sms = new FakeSms(new SmsSendResult(SmsSendStatus.Unavailable), available: false);

        var result = await new SmsNotificationSender(sms).SendAsync("abc", "+48123456789", "Test", CancellationToken.None);

        Assert.Equal(new DeliverySendResult(false, ErrorCode: "providerNotConfigured", Retryable: false), result);
        Assert.Empty(sms.Sent);
    }

    private sealed class FakeSms(SmsSendResult result, bool available = true) : ISmsSender
    {
        public List<(string Reference, string PhoneNumber, string Message)> Sent { get; } = [];

        public bool IsAvailable => available;

        public Task<SmsSendResult> SendAsync(string reference, string phoneNumber, string message, CancellationToken cancellationToken)
        {
            Sent.Add((reference, phoneNumber, message));
            return Task.FromResult(result);
        }
    }
}
