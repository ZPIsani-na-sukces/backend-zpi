using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Identity.Application.Accounts;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Identity.Domain;

namespace Zpi.IdentityTests;

public sealed partial class AccountLifecycleTests
{
    private const string ProfilePath = "/api/v1/me/profile";

    [PostgresTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Contact_change_consumes_only_the_correct_code_for_the_same_account(bool email)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        using var other = factory.CreateClient();
        var (_, phone) = await ActiveAccount(client);
        var (_, otherPhone) = await ActiveAccount(other);
        Bearer(client, await Login(client, phone));
        Bearer(other, await Login(other, otherPhone));
        var path = ContactPath(email);
        var destination = NewContact(email);
        var etag = await ProfileVersion(client);
        using var started = await StartChange(client, email, destination);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        Assert.True(started.Headers.CacheControl!.NoStore);
        var challenge = (await started.Content.ReadFromJsonAsync<ChallengeDto>())!;
        Assert.Equal(email ? "email" : "sms", challenge.Channel);
        var code = email
            ? factory.Services.GetRequiredService<TestEmailCodeSender>().CodeFor(challenge.ChallengeId)
            : factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(challenge.ChallengeId);
        var wrongCode = code == "000000" ? "111111" : "000000";

        using var foreign = await ConfirmChange(other, path, challenge.ChallengeId, code, await ProfileVersion(other));
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        using var incorrect = await ConfirmChange(client, path, challenge.ChallengeId, wrongCode, etag);
        Assert.Equal(HttpStatusCode.BadRequest, incorrect.StatusCode);
        Assert.Equal(etag, await ProfileVersion(client));

        using var confirmed = await ConfirmChange(client, path, challenge.ChallengeId, code, etag);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var account = (await confirmed.Content.ReadFromJsonAsync<AccountDto>())!;
        Assert.Equal(destination, email ? account.Email : account.PhoneNumber);
        var updatedVersion = confirmed.Headers.ETag!.Tag;
        Assert.NotEqual(etag, updatedVersion);
        Assert.Equal(updatedVersion, await ProfileVersion(client));
        using var replay = await ConfirmChange(client, path, challenge.ChallengeId, code, updatedVersion);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using var newLogin = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = destination, password = Password, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [PostgresTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Contact_change_uses_the_account_version_and_does_not_resend_a_code_for_the_current_contact(bool email)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var (_, phone) = await ActiveAccount(client);
        Bearer(client, await Login(client, phone));
        var path = ContactPath(email);
        var destination = NewContact(email);
        var etag = await ProfileVersion(client);
        using var started = await StartChange(client, email, destination);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var challenge = (await started.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var code = email
            ? factory.Services.GetRequiredService<TestEmailCodeSender>().CodeFor(challenge.ChallengeId)
            : factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(challenge.ChallengeId);

        using var missingVersion = await ConfirmChange(client, path, challenge.ChallengeId, code, null);
        Assert.Equal((HttpStatusCode)428, missingVersion.StatusCode);
        using var staleVersion = await ConfirmChange(client, path, challenge.ChallengeId, code, "\"999999\"");
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleVersion.StatusCode);
        Assert.Equal(etag, await ProfileVersion(client));
        using var confirmed = await ConfirmChange(client, path, challenge.ChallengeId, code, etag);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        using var current = await StartChange(client, email, destination);
        Assert.Equal(HttpStatusCode.Conflict, current.StatusCode);
    }

    [PostgresTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Contact_change_rejects_duplicates_throttles_resends_and_invalidates_previous_code(bool email)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        using var other = factory.CreateClient();
        var (id, phone) = await ActiveAccount(client);
        var (otherId, otherPhone) = await ActiveAccount(other);
        var otherDestination = otherPhone;
        if (email)
        {
            otherDestination = NewContact(true);
            await using var context = fixture.Context();
            var account = await context.Accounts.SingleAsync(row => row.Id == otherId);
            account.Email = otherDestination;
            account.NormalizedEmail = otherDestination.ToUpperInvariant();
            await context.SaveChangesAsync();
        }
        Bearer(client, await Login(client, phone));
        using var duplicate = await StartChange(client, email, otherDestination);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var destination = NewContact(email);
        using var started = await StartChange(client, email, destination);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var oldChallenge = (await started.Content.ReadFromJsonAsync<ChallengeDto>())!;
        using var tooSoon = await StartChange(client, email, destination);
        Assert.Equal(HttpStatusCode.TooManyRequests, tooSoon.StatusCode);

        await using var cooldownContext = fixture.Context();
        var challenge = await cooldownContext.VerificationChallenges.SingleAsync(row => row.Id == oldChallenge.ChallengeId);
        Assert.Equal(id, challenge.AccountId);
        challenge.ResendAfter = DateTimeOffset.UtcNow.AddSeconds(-1);
        await cooldownContext.SaveChangesAsync();

        using var resent = await StartChange(client, email, destination);
        Assert.Equal(HttpStatusCode.Accepted, resent.StatusCode);
        var newChallenge = (await resent.Content.ReadFromJsonAsync<ChallengeDto>())!;
        Assert.NotEqual(oldChallenge.ChallengeId, newChallenge.ChallengeId);
        var originalCode = email
            ? factory.Services.GetRequiredService<TestEmailCodeSender>().CodeFor(oldChallenge.ChallengeId)
            : factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(oldChallenge.ChallengeId);
        using var invalidated = await ConfirmChange(client, ContactPath(email), oldChallenge.ChallengeId,
            originalCode, await ProfileVersion(client));
        Assert.Equal(HttpStatusCode.BadRequest, invalidated.StatusCode);
    }

    [PostgresTheory]
    [InlineData(PhoneCodeDelivery.InvalidPhoneNumber, HttpStatusCode.BadRequest)]
    [InlineData(PhoneCodeDelivery.Unavailable, HttpStatusCode.ServiceUnavailable)]
    public async Task Phone_change_can_retry_immediately_after_delivery_fails(
        PhoneCodeDelivery failure, HttpStatusCode status)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var (_, phone) = await ActiveAccount(client);
        Bearer(client, await Login(client, phone));
        var destination = NewContact(false);
        var sender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        sender.Respond(destination, failure);
        using var failed = await StartChange(client, false, destination);
        Assert.Equal(status, failed.StatusCode);

        sender.Respond(destination, PhoneCodeDelivery.Sent);
        using var retried = await StartChange(client, false, destination);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
    }

    [PostgresTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Contact_change_rechecks_uniqueness_before_consuming_the_code(bool email)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        using var other = factory.CreateClient();
        var (_, phone) = await ActiveAccount(client);
        var (otherId, _) = await ActiveAccount(other);
        Bearer(client, await Login(client, phone));
        var destination = NewContact(email);
        using var started = await StartChange(client, email, destination);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var challenge = (await started.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var code = email
            ? factory.Services.GetRequiredService<TestEmailCodeSender>().CodeFor(challenge.ChallengeId)
            : factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(challenge.ChallengeId);

        await using var db = fixture.Context();
        var account = await db.Accounts.SingleAsync(row => row.Id == otherId);
        if (email)
        {
            account.Email = destination;
            account.NormalizedEmail = destination.ToUpperInvariant();
        }
        else
        {
            account.PhoneNumber = destination;
            account.NormalizedPhoneNumber = destination;
        }
        await db.SaveChangesAsync();

        var version = await ProfileVersion(client);
        using var conflict = await ConfirmChange(client, ContactPath(email), challenge.ChallengeId, code, version);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(version, await ProfileVersion(client));
        var stored = await db.VerificationChallenges.SingleAsync(row => row.Id == challenge.ChallengeId);
        Assert.Null(stored.ConsumedAt);
    }

    [PostgresTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Contact_change_can_start_after_an_unavailable_channel_recovers(bool email)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var (_, phone) = await ActiveAccount(client);
        Bearer(client, await Login(client, phone));
        var destination = NewContact(email);
        var phoneSender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        var emailSender = factory.Services.GetRequiredService<TestEmailCodeSender>();
        phoneSender.IsAvailable = email;
        emailSender.IsAvailable = !email;
        using var unavailable = await StartChange(client, email, destination);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);

        phoneSender.IsAvailable = true;
        emailSender.IsAvailable = true;
        using var recovered = await StartChange(client, email, destination);
        Assert.Equal(HttpStatusCode.Accepted, recovered.StatusCode);
    }

    [PostgresFact]
    public async Task Email_change_returns_delivery_unavailable_without_altering_the_account_when_sending_fails()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var (id, phone) = await ActiveAccount(client);
        Bearer(client, await Login(client, phone));
        var sender = factory.Services.GetRequiredService<TestEmailCodeSender>();
        sender.DeliveryError = new EmailDeliveryException();

        var version = await ProfileVersion(client);
        using var response = await StartChange(client, true, NewContact(true));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("deliveryUnavailable", await response.Content.ReadAsStringAsync());
        Assert.Equal(version, await ProfileVersion(client));
        await using var db = fixture.Context();
        Assert.Null(await db.Accounts.Where(row => row.Id == id).Select(row => row.Email).SingleAsync());
    }

    private static string NewContact(bool email) => email
        ? $"account-{Guid.CreateVersion7():N}@example.test"
        : "+48" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);

    private static string ContactPath(bool email) => ProfilePath + (email ? "/email" : "/phone-number");

    private static Task<HttpResponseMessage> StartChange(HttpClient client, bool email, string destination) =>
        client.PostAsJsonAsync(ContactPath(email) + "/change-requests",
            email ? (object)new { email = destination } : new { phoneNumber = destination });

    private static async Task<HttpResponseMessage> ConfirmChange(HttpClient client, string path, Guid challengeId,
        string code, string? etag)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(new { challengeId, code })
        };
        if (etag is not null) request.Headers.IfMatch.ParseAdd(etag);
        return await client.SendAsync(request);
    }

    private static async Task<string> ProfileVersion(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.ETag!.Tag;
    }
}
