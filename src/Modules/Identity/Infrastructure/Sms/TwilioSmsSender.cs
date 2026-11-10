using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class TwilioSmsSender(IHttpClientFactory clients, IOptions<TwilioOptions> options,
    ILogger<TwilioSmsSender> logger) : ISmsSender
{
    public const string ClientName = "identity-twilio";

    private static readonly HashSet<int> UndeliverableNumberErrors = [21211, 21408, 21610, 21612, 21614];

    public bool IsAvailable => true;

    public async Task<SmsSendResult> SendAsync(string reference, string phoneNumber, string message, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var fields = new Dictionary<string, string>
        {
            ["To"] = phoneNumber,
            ["From"] = settings.From,
            ["Body"] = message
        };

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{settings.AccountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(fields)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.AccountSid}:{settings.AuthToken}")));

        try
        {
            using var client = clients.CreateClient(ClientName);

            using var response = await client.SendAsync(request, cancellationToken);

            var body = await ReadJson(response, cancellationToken);

            if (response.IsSuccessStatusCode) return new SmsSendResult(SmsSendStatus.Sent, ReadMessageSid(body));

            var errorCode = ReadErrorCode(body);

            logger.Log(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? LogLevel.Error : LogLevel.Warning,
                "Twilio rejected the SMS for {Reference}: HTTP {StatusCode}, Twilio error {TwilioErrorCode}.",
                reference, (int)response.StatusCode, errorCode);

            return new SmsSendResult(errorCode is { } known && UndeliverableNumberErrors.Contains(known)
                ? SmsSendStatus.InvalidPhoneNumber
                : SmsSendStatus.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Twilio timed out sending the SMS for {Reference}.", reference);
            return new SmsSendResult(SmsSendStatus.Unavailable);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach Twilio to send the SMS for {Reference}.", reference);
            return new SmsSendResult(SmsSendStatus.Unavailable);
        }
    }

    private static async Task<JsonElement?> ReadJson(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadMessageSid(JsonElement? body) =>
        body is { } json && json.TryGetProperty("sid", out var sid) && sid.ValueKind == JsonValueKind.String ? sid.GetString() : null;

    private static int? ReadErrorCode(JsonElement? body) =>
        body is { } json && json.TryGetProperty("code", out var code) &&
        code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var value) ? value : null;
}
