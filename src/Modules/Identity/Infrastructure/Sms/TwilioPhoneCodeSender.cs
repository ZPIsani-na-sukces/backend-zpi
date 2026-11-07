using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class TwilioPhoneCodeSender(IHttpClientFactory clients, IOptions<TwilioOptions> options,
    ILogger<TwilioPhoneCodeSender> logger) : IPhoneCodeSender
{
    public const string ClientName = "identity-twilio";

    private static readonly HashSet<int> UndeliverableNumberErrors = [21211, 21408, 21610, 21612, 21614];

    public bool IsAvailable => true;

    public async Task<PhoneCodeDelivery> SendAsync(Guid challengeId, string phoneNumber, string code, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var fields = new Dictionary<string, string>
        {
            ["To"] = phoneNumber,
            ["From"] = settings.From,
            ["Body"] = settings.MessageTemplate.Replace("{code}", code, StringComparison.Ordinal)
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

            if (response.IsSuccessStatusCode) return PhoneCodeDelivery.Sent;

            var errorCode = await ReadErrorCode(response, cancellationToken);

            logger.Log(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? LogLevel.Error : LogLevel.Warning,
                "Twilio rejected the SMS for challenge {ChallengeId}: HTTP {StatusCode}, Twilio error {TwilioErrorCode}.",
                challengeId, (int)response.StatusCode, errorCode);

            return errorCode is { } known && UndeliverableNumberErrors.Contains(known)
                ? PhoneCodeDelivery.InvalidPhoneNumber
                : PhoneCodeDelivery.Unavailable;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Twilio timed out sending the SMS for challenge {ChallengeId}.", challengeId);
            return PhoneCodeDelivery.Unavailable;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach Twilio to send the SMS for challenge {ChallengeId}.", challengeId);
            return PhoneCodeDelivery.Unavailable;
        }
    }

    private static async Task<int?> ReadErrorCode(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("code", out var code) &&
                code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var value) ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
