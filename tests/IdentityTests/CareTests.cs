using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Npgsql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zpi.Modules.Care.Application.Invitations.Cleanup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Domain;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Shared.Pagination;
using Zpi.Modules.Care.Application.Relationships;
using Zpi.Modules.Care.Application.Relationships.ListSeniors;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class CareTests(IdentityApiFixture fixture)
{
    private const string Password = "A long password 123!";

    [PostgresTheory]
    [InlineData(AccountRole.Caretaker)]
    [InlineData(AccountRole.Senior)]
    public async Task Either_role_can_invite_preview_discloses_only_first_name_and_role_and_acceptance_grants_access(AccountRole senderRole)
    {
        await using var factory = fixture.Factory();
        using var sender = await Actor(factory, senderRole);
        using var recipient = await Actor(factory, senderRole == AccountRole.Senior ? AccountRole.Caretaker : AccountRole.Senior);
        var invitation = await Invite(sender);
        Assert.Matches("^[0-9]{6}$", invitation.Code);
        Assert.Equal("pending", invitation.Status);
        Assert.Equal(TimeSpan.FromMinutes(15), invitation.ExpiresAt - invitation.CreatedAt);
        using var preview = await recipient.Client.GetAsync($"/api/v1/care-invitations/{invitation.Code}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.True(preview.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "createdAt", "expiresAt", "sender", "status" }, json.RootElement.EnumerateObject().Select(row => row.Name).Order().ToArray());
        var senderJson = json.RootElement.GetProperty("sender");
        Assert.Equal(new[] { "firstName", "role" }, senderJson.EnumerateObject().Select(row => row.Name).Order().ToArray());
        Assert.Equal("Jan", senderJson.GetProperty("firstName").GetString());
        Assert.Equal(senderRole == AccountRole.Senior ? "senior" : "caretaker", senderJson.GetProperty("role").GetString());
        await using (var context = CareContext())
        {
            var pending = await context.SeniorCareInvitations.SingleAsync(row => row.Code == invitation.Code);
            Assert.Null(pending.RecipientAccountId);
            Assert.Null(pending.RelationshipId);
            Assert.Equal(InvitationStatus.Pending, pending.Status);
        }
        var relationship = await Accept(recipient, invitation.Code);
        var senior = senderRole == AccountRole.Senior ? sender : recipient;
        var caretaker = senderRole == AccountRole.Caretaker ? sender : recipient;
        Assert.Equal(senior.Id, relationship.Senior.UserId);
        Assert.Equal(caretaker.Id, relationship.Caretaker.UserId);
        Assert.Equal(relationship, await Accept(recipient, invitation.Code));
        await using var scope = factory.Services.CreateAsyncScope();
        var care = scope.ServiceProvider.GetRequiredService<ICareFacade>();
        Assert.True(await care.HasActiveRelationshipAsync(caretaker.Id, senior.Id, CancellationToken.None));
        Assert.Equal(HttpStatusCode.NoContent, (await senior.Client.DeleteAsync($"/api/v1/care-relationships/{relationship.RelationshipId}")).StatusCode);
        Assert.False(await care.HasActiveRelationshipAsync(caretaker.Id, senior.Id, CancellationToken.None));
        var repeated = await Accept(recipient, invitation.Code);
        Assert.Equal(relationship.RelationshipId, repeated.RelationshipId);
        Assert.Equal("removed", repeated.Status);
        Assert.Equal(HttpStatusCode.NoContent, (await caretaker.Client.DeleteAsync($"/api/v1/care-relationships/{relationship.RelationshipId}")).StatusCode);
        await Error(await factory.CreateClient().GetAsync($"/api/v1/care-invitations/{invitation.Code}"), 401, "unauthenticated");
    }

    [PostgresFact]
    public async Task Generating_reuses_pending_code_without_extending_lifetime_and_consumption_allows_a_new_code()
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var senior = await Actor(factory, AccountRole.Senior);
        var first = await Invite(caretaker);
        var duplicate = await caretaker.Client.PostAsync("/api/v1/care-invitations", null);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(first, await duplicate.Content.ReadFromJsonAsync<CareInvitationDto>());
        await Accept(senior, first.Code);
        var next = await Invite(caretaker);
        Assert.NotEqual(first.Code, next.Code);
        await Error(await senior.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = next.Code }), 409, "relationshipAlreadyExists");
        await using var context = CareContext();
        Assert.Equal(InvitationStatus.Pending, (await context.SeniorCareInvitations.SingleAsync(row => row.Code == next.Code)).Status);
    }

    [PostgresFact]
    public async Task Own_codes_same_roles_consumed_codes_and_inactive_senders_are_rejected()
    {
        await using var factory = fixture.Factory();
        using var sender = await Actor(factory, AccountRole.Caretaker);
        using var sameRole = await Actor(factory, AccountRole.Caretaker);
        using var recipient = await Actor(factory, AccountRole.Senior);
        using var otherRecipient = await Actor(factory, AccountRole.Senior);
        var invitation = await Invite(sender);
        foreach (var actor in new[] { sender, sameRole })
        {
            await Error(await actor.Client.GetAsync($"/api/v1/care-invitations/{invitation.Code}"), 403, "operationForbidden");
            await Error(await actor.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = invitation.Code }), 403, "operationForbidden");
        }
        await Accept(recipient, invitation.Code);
        await Error(await otherRecipient.Client.GetAsync($"/api/v1/care-invitations/{invitation.Code}"), 409, "invalidState");
        await Error(await otherRecipient.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = invitation.Code }), 409, "invalidState");
        var pending = await Invite(sender);
        await using (var context = fixture.Context())
        {
            var account = await context.Accounts.SingleAsync(row => row.Id == sender.Id);
            account.Status = AccountStatus.Blocked;
            await context.SaveChangesAsync();
        }
        await Error(await recipient.Client.GetAsync($"/api/v1/care-invitations/{pending.Code}"), 404, "resourceNotFound");
        await Error(await recipient.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = pending.Code }), 404, "resourceNotFound");
        await Error(await sender.Client.PostAsync("/api/v1/care-invitations", null), 401, "unauthenticated");
    }

    [PostgresFact]
    public async Task Exact_expiration_blocks_preview_and_acceptance_and_cleanup_preserves_relationships()
    {
        var clock = new TestClock();
        await using var factory = fixture.Factory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(clock)));
        using var sender = await Actor(factory, AccountRole.Caretaker);
        using var senior = await Actor(factory, AccountRole.Senior);
        var accepted = await Invite(sender);
        var relationship = await Accept(senior, accepted.Code);
        var pending = await Invite(sender);
        clock.Now = accepted.ExpiresAt.AddTicks(-1);
        Assert.Equal(HttpStatusCode.OK, (await senior.Client.GetAsync($"/api/v1/care-invitations/{pending.Code}")).StatusCode);
        clock.Now = accepted.ExpiresAt;
        foreach (var invitation in new[] { accepted, pending })
        {
            await Error(await senior.Client.GetAsync($"/api/v1/care-invitations/{invitation.Code}"), 404, "resourceNotFound");
            await Error(await senior.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = invitation.Code }), 404, "resourceNotFound");
        }
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<InvitationCleanupProcessor>().ProcessAsync(CancellationToken.None);
        await using var context = CareContext();
        Assert.False(await context.SeniorCareInvitations.AnyAsync(row => row.Code == accepted.Code || row.Code == pending.Code));
        Assert.Equal(RelationshipStatus.Active, (await context.SeniorCareRelationships.SingleAsync(row => row.Id == relationship.RelationshipId)).Status);
        Assert.True(await scope.ServiceProvider.GetRequiredService<ICareFacade>().HasActiveRelationshipAsync(sender.Id, senior.Id, CancellationToken.None));
        var next = await Invite(sender);
        Assert.Equal(clock.Now, next.CreatedAt);
        await scope.ServiceProvider.GetRequiredService<InvitationCleanupProcessor>().ProcessAsync(CancellationToken.None);
        Assert.True(await context.SeniorCareInvitations.AnyAsync(row => row.Code == next.Code));
    }

    [PostgresFact]
    public async Task Generation_replaces_expired_pending_even_before_cleanup_runs()
    {
        var clock = new TestClock();
        await using var factory = fixture.Factory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(clock)));
        using var senior = await Actor(factory, AccountRole.Senior);
        var first = await Invite(senior);
        clock.Now = first.ExpiresAt;
        var next = await Invite(senior);
        Assert.Equal(clock.Now, next.CreatedAt);
        await using var context = CareContext();
        var row = Assert.Single(await context.SeniorCareInvitations.Where(row => row.SenderAccountId == senior.Id).ToListAsync());
        Assert.Equal(next.Code, row.Code);
        Assert.Equal(next.CreatedAt, row.CreatedAt);
    }

    [PostgresFact]
    public async Task Code_collisions_retry_without_losing_existing_invitations()
    {
        await using var factory = fixture.Factory();
        using var sender = await Actor(factory, AccountRole.Caretaker);
        var existing = await Invite(sender);
        string unused;
        await using (var context = CareContext())
        {
            var codes = await context.SeniorCareInvitations.Select(row => row.Code).ToListAsync();
            unused = Enumerable.Range(0, 1_000_000).Select(value => value.ToString("D6")).First(code => !codes.Contains(code));
        }
        var generator = new SequenceCodeGenerator(existing.Code, existing.Code, unused);
        await using var collisionFactory = fixture.Factory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IInvitationCodeGenerator>(generator)));
        using var otherSender = await Actor(collisionFactory, AccountRole.Senior);
        var created = await Invite(otherSender);
        Assert.Equal(unused, created.Code);
        Assert.Equal(3, generator.Calls);
        await using var after = CareContext();
        Assert.True(await after.SeniorCareInvitations.AnyAsync(row => row.Code == existing.Code && row.SenderAccountId == sender.Id));
        Assert.True(await after.SeniorCareInvitations.AnyAsync(row => row.Code == unused && row.SenderAccountId == otherSender.Id));
    }

    [PostgresFact]
    public async Task Concurrent_generation_and_acceptance_have_one_pending_code_and_one_winning_recipient()
    {
        await using var factory = fixture.Factory();
        using var sender = await Actor(factory, AccountRole.Caretaker);
        using var first = await Actor(factory, AccountRole.Senior);
        using var second = await Actor(factory, AccountRole.Senior);
        var created = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => sender.Client.PostAsync("/api/v1/care-invitations", null)));
        Assert.Contains(created, response => response.StatusCode == HttpStatusCode.Created);
        Assert.All(created, response => Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.Conflict));
        var codes = new List<string>();
        foreach (var response in created.Where(row => row.IsSuccessStatusCode))
            codes.Add((await response.Content.ReadFromJsonAsync<CareInvitationDto>())!.Code);
        var code = Assert.Single(codes.Distinct());
        var attempts = new[] { first, second, first, second, first, second };
        var responses = await Task.WhenAll(attempts.Select(actor => actor.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code })));
        Assert.Contains(responses, response => response.IsSuccessStatusCode);
        Assert.All(responses, response => Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        var relationships = new List<CareRelationshipDto>();
        foreach (var response in responses.Where(row => row.IsSuccessStatusCode))
            relationships.Add((await response.Content.ReadFromJsonAsync<CareRelationshipDto>())!);
        var winner = Assert.Single(relationships.Select(row => row.Senior.UserId).Distinct());
        var relationshipId = Assert.Single(relationships.Select(row => row.RelationshipId).Distinct());
        Assert.Equal(relationshipId, (await Accept(winner == first.Id ? first : second, code)).RelationshipId);
        await using var context = CareContext();
        Assert.Equal(1, await context.SeniorCareInvitations.CountAsync(row => row.SenderAccountId == sender.Id));
        Assert.Equal(1, await context.SeniorCareRelationships.CountAsync(row => row.CaretakerId == sender.Id));
    }

    [PostgresTheory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData("１２３４５６")]
    [InlineData("123456\n")]
    [InlineData(" 123456")]
    [InlineData(null)]
    public async Task Invalid_codes_use_shared_validation(string? code)
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        await Error(await senior.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code }), 400, "validationFailed");
        if (!string.IsNullOrEmpty(code))
            await Error(await senior.Client.GetAsync($"/api/v1/care-invitations/{Uri.EscapeDataString(code)}"), 400, "validationFailed");
    }

    [PostgresFact]
    public async Task Generated_openapi_describes_the_code_route_and_both_generation_statuses()
    {
        await using var factory = fixture.Factory().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var invitations = paths.EnumerateObject().Where(row => row.Name.Contains("care-invitations")).ToArray();
        Assert.Equal(3, invitations.Length);
        var generation = paths.GetProperty("/api/v1/care-invitations").GetProperty("post");
        Assert.False(generation.TryGetProperty("requestBody", out _));
        Assert.True(generation.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(generation.GetProperty("responses").TryGetProperty("201", out _));
        var preview = paths.GetProperty("/api/v1/care-invitations/{code}").GetProperty("get");
        var parameter = Assert.Single(preview.GetProperty("parameters").EnumerateArray());
        Assert.Equal("code", parameter.GetProperty("name").GetString());
        Assert.Equal("path", parameter.GetProperty("in").GetString());
        Assert.True(parameter.GetProperty("required").GetBoolean());
        Assert.True(paths.GetProperty("/api/v1/care-invitations/accept").GetProperty("post").TryGetProperty("requestBody", out _));
    }

    [PostgresTheory]
    [InlineData("/api/v1/me/seniors?page=0")]
    [InlineData("/api/v1/me/seniors?page=-1")]
    [InlineData("/api/v1/me/seniors?page=2147483647")]
    [InlineData("/api/v1/me/seniors?limit=0")]
    [InlineData("/api/v1/me/seniors?limit=101")]
    public async Task Invalid_relationship_pagination_uses_shared_validation(string path)
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Error(await caretaker.Client.GetAsync(path), 400, "validationFailed");
    }

    [PostgresFact]
    public async Task Missing_codes_unknown_codes_and_removed_endpoints_do_not_use_the_old_flow()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        await Error(await senior.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { }), 400, "validationFailed");
        string unused;
        await using (var context = CareContext())
        {
            var codes = await context.SeniorCareInvitations.Select(row => row.Code).ToListAsync();
            unused = Enumerable.Range(0, 1_000_000).Select(value => value.ToString("D6")).First(code => !codes.Contains(code));
        }
        await Error(await senior.Client.GetAsync($"/api/v1/care-invitations/{unused}"), 404, "resourceNotFound");
        await Error(await senior.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = unused }), 404, "resourceNotFound");
        Assert.Equal(HttpStatusCode.NotFound, (await senior.Client.GetAsync("/api/v1/me/care-invitations")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await senior.Client.PostAsync($"/api/v1/care-invitations/{Guid.CreateVersion7()}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await senior.Client.PostAsync($"/api/v1/care-invitations/{Guid.CreateVersion7()}/reject", null)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await senior.Client.DeleteAsync($"/api/v1/care-invitations/{Guid.CreateVersion7()}")).StatusCode);
    }

    [PostgresTheory]
    [InlineData(AccountRole.Senior)]
    [InlineData(AccountRole.Caretaker)]
    public async Task Deactivation_denies_access_and_account_cleanup_deletes_own_and_received_invitations(AccountRole deletedRole)
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var senior = await Actor(factory, AccountRole.Senior);
        var invitation = await Invite(caretaker);
        var relationship = await Accept(senior, invitation.Code);
        await Invite(senior);
        var pendingCaretaker = await Invite(caretaker);
        var deleted = deletedRole == AccountRole.Senior ? senior : caretaker;
        await using (var context = fixture.Context())
        {
            var account = await context.Accounts.SingleAsync(row => row.Id == deleted.Id);
            account.Status = AccountStatus.PendingDeletion;
            account.DeletionRequestedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ICareFacade>().HasActiveRelationshipAsync(caretaker.Id, senior.Id, CancellationToken.None));
        await Error(await deleted.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = pendingCaretaker.Code }), 401, "unauthenticated");
        foreach (var cleanup in scope.ServiceProvider.GetServices<IAccountDataCleanup>())
            await cleanup.DeleteAsync(deleted.Id, CancellationToken.None);
        await using var care = CareContext();
        Assert.False(await care.SeniorCareInvitations.AnyAsync(row => row.SenderAccountId == deleted.Id || row.RecipientAccountId == deleted.Id));
        Assert.False(await care.SeniorCareRelationships.AnyAsync(row => row.Id == relationship.RelationshipId));
    }

    [PostgresFact]
    public async Task Migration_discards_legacy_invitations_and_preserves_existing_relationships()
    {
        var name = "zpi_care_migration_" + Guid.CreateVersion7().ToString("N");
        await using var admin = new NpgsqlConnection(fixture.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin))
            await create.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name };
        try
        {
            var options = new DbContextOptionsBuilder<CareDbContext>();
            DatabaseConfiguration.Configure(options, builder.ConnectionString, CareDbContext.Schema);
            await using var context = new CareDbContext(options.Options);
            await context.GetService<IMigrator>().MigrateAsync("20261006133301_InitialCreate");
            var senior = Guid.CreateVersion7();
            var caretaker = Guid.CreateVersion7();
            var invitation = Guid.CreateVersion7();
            var relationship = Guid.CreateVersion7();
            var now = DateTimeOffset.UtcNow;
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO care.senior_care_invitation (id, senior_id, caretaker_id, sender_account_id, recipient_account_id, status, created_at, expires_at, responded_at, version) VALUES ({invitation}, {senior}, {caretaker}, {caretaker}, {senior}, 'accepted', {now}, {now.AddDays(7)}, {now}, 1)");
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO care.senior_care_relationship (id, invitation_id, senior_id, caretaker_id, status, created_at, version) VALUES ({relationship}, {invitation}, {senior}, {caretaker}, 'active', {now}, 1)");
            await context.Database.MigrateAsync();
            Assert.Empty(await context.SeniorCareInvitations.ToListAsync());
            var kept = Assert.Single(await context.SeniorCareRelationships.ToListAsync());
            Assert.Equal(relationship, kept.Id);
            Assert.Equal(senior, kept.SeniorId);
            Assert.Equal(caretaker, kept.CaretakerId);
            Assert.Equal(RelationshipStatus.Active, kept.Status);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    public async Task Pagination_uses_numbered_pages_and_preserves_search_filters_and_access()
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var otherCaretaker = await Actor(factory, AccountRole.Caretaker);
        using var first = await Actor(factory, AccountRole.Senior, "Aaa");
        using var second = await Actor(factory, AccountRole.Senior, "Bbb");
        using var third = await Actor(factory, AccountRole.Senior, "Ccc");
        foreach (var senior in new[] { third, first, second })
            await Accept(senior, (await Invite(caretaker)).Code);
        var page = (await caretaker.Client.GetFromJsonAsync<PageDto<SeniorSummaryDto>>("/api/v1/me/seniors?limit=1"))!;
        Assert.Equal(first.Id, Assert.Single(page.Items).Senior.UserId);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.Limit);
        Assert.Equal(3, page.TotalCount);
        var next = (await caretaker.Client.GetFromJsonAsync<PageDto<SeniorSummaryDto>>("/api/v1/me/seniors?page=2&limit=1"))!;
        Assert.Equal(second.Id, Assert.Single(next.Items).Senior.UserId);
        Assert.Equal(3, next.TotalCount);
        Assert.Empty((await caretaker.Client.GetFromJsonAsync<PageDto<SeniorSummaryDto>>("/api/v1/me/seniors?page=4&limit=1"))!.Items);
        Assert.Empty((await otherCaretaker.Client.GetFromJsonAsync<PageDto<SeniorSummaryDto>>("/api/v1/me/seniors?page=2&limit=1"))!.Items);
        var filtered = (await caretaker.Client.GetFromJsonAsync<PageDto<SeniorSummaryDto>>("/api/v1/me/seniors?search=bbb"))!;
        Assert.Equal(second.Id, Assert.Single(filtered.Items).Senior.UserId);
        Assert.Equal(1, filtered.TotalCount);
        var caretakers = (await first.Client.GetFromJsonAsync<PageDto<CareRelationshipDto>>($"/api/v1/seniors/{first.Id}/caretakers?limit=1"))!;
        Assert.Equal(caretaker.Id, Assert.Single(caretakers.Items).Caretaker.UserId);
        Assert.Equal(1, caretakers.TotalCount);
        Assert.Empty((await first.Client.GetFromJsonAsync<PageDto<CareRelationshipDto>>($"/api/v1/seniors/{first.Id}/caretakers?page=2&limit=1"))!.Items);
    }

    private async Task<TestActor> Actor(WebApplicationFactory<Program> factory, AccountRole role, string lastName = "Kowalski")
    {
        var phone = "+48" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
        var email = Guid.CreateVersion7() + "@example.com";
        await using var context = fixture.Context();
        var now = DateTimeOffset.UtcNow;
        var account = new Account
        {
            RoleId = await context.Roles.Where(row => row.Code == role).Select(row => row.Id).SingleAsync(),
            PhoneNumber = phone, NormalizedPhoneNumber = phone, Email = email, NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = "", FirstName = "Jan", LastName = lastName,
            Status = AccountStatus.Active, PhoneVerifiedAt = now, EmailVerifiedAt = now, CreatedAt = now, UpdatedAt = now
        };
        account.PasswordHash = factory.Services.GetRequiredService<PasswordHashing>().Hash(account, Password);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new TestActor(account.Id, phone, email, client);
    }

    private static async Task<CareInvitationDto> Invite(TestActor sender)
    {
        var response = await sender.Client.PostAsync("/api/v1/care-invitations", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<CareInvitationDto>())!;
    }

    private static async Task<CareRelationshipDto> Accept(TestActor recipient, string code)
    {
        var response = await recipient.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<CareRelationshipDto>())!;
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class SequenceCodeGenerator(params string[] codes) : IInvitationCodeGenerator
    {
        public int Calls { get; private set; }
        public string Generate() => codes[Calls++];
    }

    private static async Task Error(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
    }

    private CareDbContext CareContext()
    {
        var options = new DbContextOptionsBuilder<CareDbContext>();
        DatabaseConfiguration.Configure(options, fixture.ConnectionString, CareDbContext.Schema);
        return new CareDbContext(options.Options);
    }

    private sealed record TestActor(Guid Id, string Phone, string Email, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
