using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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

    [PostgresFact]
    public async Task Pairing_grants_access_and_removal_revokes_it_without_losing_history()
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var senior = await Actor(factory, AccountRole.Senior);
        var invitation = await Invite(caretaker, senior.Email);
        Assert.Equal(TimeSpan.FromDays(7), invitation.ExpiresAt - invitation.CreatedAt);
        Assert.Equal("caretaker", invitation.Sender.Role);
        Assert.Equal("senior", invitation.Recipient.Role);
        Assert.Equal(senior.Id, invitation.Recipient.UserId);
        var received = await senior.Client.GetFromJsonAsync<PageDto<CareInvitationDto>>("/api/v1/me/care-invitations");
        Assert.Equal(invitation.InvitationId, Assert.Single(received!.Items).InvitationId);
        var sent = await caretaker.Client.GetFromJsonAsync<PageDto<CareInvitationDto>>("/api/v1/me/care-invitations?direction=sent");
        Assert.Equal(invitation.InvitationId, Assert.Single(sent!.Items).InvitationId);

        var relationship = await Accept(senior, invitation.InvitationId);
        var replay = await Accept(senior, invitation.InvitationId);
        Assert.Equal(relationship, replay);
        await using var scope = factory.Services.CreateAsyncScope();
        var facade = scope.ServiceProvider.GetRequiredService<ICareFacade>();
        Assert.True(await facade.HasActiveRelationshipAsync(caretaker.Id, senior.Id, CancellationToken.None));
        Assert.True(await facade.CanAccessSeniorAsync(senior.Id, senior.Id, CancellationToken.None));
        var seniors = await caretaker.Client.GetFromJsonAsync<PageDto<SeniorSummaryDto>>("/api/v1/me/seniors");
        Assert.Equal(senior.Id, Assert.Single(seniors!.Items).Senior.UserId);
        var caretakers = await senior.Client.GetFromJsonAsync<PageDto<CareRelationshipDto>>($"/api/v1/seniors/{senior.Id}/caretakers");
        Assert.Equal(caretaker.Id, Assert.Single(caretakers!.Items).Caretaker.UserId);
        Assert.Equal(HttpStatusCode.NoContent, (await caretaker.Client.DeleteAsync($"/api/v1/care-relationships/{relationship.RelationshipId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await senior.Client.DeleteAsync($"/api/v1/care-relationships/{relationship.RelationshipId}")).StatusCode);
        Assert.False(await facade.CanAccessSeniorAsync(caretaker.Id, senior.Id, CancellationToken.None));
        Assert.Equal(HttpStatusCode.NotFound, (await caretaker.Client.GetAsync($"/api/v1/seniors/{senior.Id}/caretakers")).StatusCode);
        Assert.Empty((await caretaker.Client.GetFromJsonAsync<PageDto<SeniorSummaryDto>>("/api/v1/me/seniors"))!.Items);
        var old = await Accept(senior, invitation.InvitationId);
        Assert.Equal("removed", old.Status);
        Assert.Equal(relationship.RelationshipId, old.RelationshipId);
        var next = await Invite(caretaker, senior.Phone);
        var replacement = await Accept(senior, next.InvitationId);
        Assert.NotEqual(relationship.RelationshipId, replacement.RelationshipId);
        await using var context = CareContext();
        Assert.Equal(2, await context.SeniorCareRelationships.CountAsync(row => row.SeniorId == senior.Id && row.CaretakerId == caretaker.Id));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Foreign_resources_and_unavailable_recipients_do_not_leak_data()
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var stranger = await Actor(factory, AccountRole.Senior);
        var invitation = await Invite(caretaker, senior.Email.ToUpperInvariant());
        foreach (var path in new[] { $"/api/v1/care-invitations/{invitation.InvitationId}", $"/api/v1/seniors/{senior.Id}/caretakers" })
            await Error(await stranger.Client.GetAsync(path), 404, "resourceNotFound");
        await Error(await stranger.Client.PostAsync($"/api/v1/care-invitations/{invitation.InvitationId}/accept", null), 404, "resourceNotFound");
        await Error(await stranger.Client.PostAsync($"/api/v1/care-invitations/{invitation.InvitationId}/reject", null), 404, "resourceNotFound");
        await Error(await senior.Client.PostAsJsonAsync("/api/v1/care-invitations", new { recipientIdentifier = stranger.Email }), 403, "operationForbidden");
        await Error(await caretaker.Client.PostAsJsonAsync("/api/v1/care-invitations", new { recipientIdentifier = caretaker.Email }), 404, "resourceNotFound");
        await Error(await caretaker.Client.PostAsJsonAsync("/api/v1/care-invitations", new { recipientIdentifier = Guid.CreateVersion7() + "@example.com" }), 404, "resourceNotFound");
        var relationship = await Accept(senior, invitation.InvitationId);
        await Error(await stranger.Client.DeleteAsync($"/api/v1/care-relationships/{relationship.RelationshipId}"), 404, "resourceNotFound");
        await Error(await caretaker.Client.PostAsJsonAsync("/api/v1/care-invitations", new { recipientIdentifier = senior.Email }), 409, "relationshipAlreadyExists");
        await using var database = fixture.Context();
        var account = await database.Accounts.SingleAsync(row => row.Id == senior.Id);
        account.Status = AccountStatus.Blocked;
        await database.SaveChangesAsync();
        await Error(await caretaker.Client.GetAsync($"/api/v1/care-invitations/{invitation.InvitationId}"), 404, "resourceNotFound");
        await Error(await caretaker.Client.PostAsJsonAsync("/api/v1/care-invitations", new { recipientIdentifier = senior.Phone }), 404, "resourceNotFound");
    }

    [PostgresFact]
    public async Task Cancellation_and_rejection_are_repeatable_but_cannot_be_accepted()
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var senior = await Actor(factory, AccountRole.Senior);
        var invitation = await Invite(caretaker, senior.Phone);
        var duplicate = await Invite(caretaker, senior.Email);
        Assert.Equal(invitation.InvitationId, duplicate.InvitationId);
        Assert.Equal(HttpStatusCode.NoContent, (await caretaker.Client.DeleteAsync($"/api/v1/care-invitations/{invitation.InvitationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await caretaker.Client.DeleteAsync($"/api/v1/care-invitations/{invitation.InvitationId}")).StatusCode);
        await Error(await senior.Client.PostAsync($"/api/v1/care-invitations/{invitation.InvitationId}/accept", null), 409, "invalidState");
        var next = await Invite(caretaker, senior.Email);
        var rejected = await senior.Client.PostAsync($"/api/v1/care-invitations/{next.InvitationId}/reject", null);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("rejected", (await rejected.Content.ReadFromJsonAsync<CareInvitationDto>())!.Status);
        Assert.Equal(HttpStatusCode.OK, (await senior.Client.PostAsync($"/api/v1/care-invitations/{next.InvitationId}/reject", null)).StatusCode);
        await Error(await senior.Client.PostAsync($"/api/v1/care-invitations/{next.InvitationId}/accept", null), 409, "invalidState");
        await Error(await caretaker.Client.DeleteAsync($"/api/v1/care-invitations/{next.InvitationId}"), 409, "invalidState");
    }

    [PostgresFact]
    public async Task Expiration_is_visible_and_does_not_block_a_new_invitation()
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var stranger = await Actor(factory, AccountRole.Senior);
        var invitation = await Invite(caretaker, senior.Email);
        await using (var context = CareContext())
        {
            var row = await context.SeniorCareInvitations.SingleAsync(row => row.Id == invitation.InvitationId);
            row.CreatedAt = DateTimeOffset.UtcNow.AddDays(-8);
            row.ExpiresAt = row.CreatedAt.AddDays(7);
            await context.SaveChangesAsync();
        }
        var detail = await senior.Client.GetFromJsonAsync<CareInvitationDto>($"/api/v1/care-invitations/{invitation.InvitationId}");
        Assert.Equal("expired", detail!.Status);
        var expired = await senior.Client.GetFromJsonAsync<PageDto<CareInvitationDto>>("/api/v1/me/care-invitations?status=expired");
        Assert.Equal(invitation.InvitationId, Assert.Single(expired!.Items).InvitationId);
        Assert.Empty((await senior.Client.GetFromJsonAsync<PageDto<CareInvitationDto>>("/api/v1/me/care-invitations?status=pending"))!.Items);
        await Error(await stranger.Client.PostAsync($"/api/v1/care-invitations/{invitation.InvitationId}/accept", null), 404, "resourceNotFound");
        await Error(await senior.Client.PostAsync($"/api/v1/care-invitations/{invitation.InvitationId}/accept", null), 410, "invitationExpired");
        await Error(await senior.Client.PostAsync($"/api/v1/care-invitations/{invitation.InvitationId}/reject", null), 410, "invitationExpired");
        Assert.NotEqual(invitation.InvitationId, (await Invite(caretaker, senior.Phone)).InvitationId);
    }

    [PostgresFact]
    public async Task Concurrent_create_and_accept_produce_one_invitation_and_one_relationship()
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var senior = await Actor(factory, AccountRole.Senior);
        var created = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => caretaker.Client.PostAsJsonAsync(
            "/api/v1/care-invitations", new { recipientIdentifier = senior.Phone })));
        Assert.Contains(created, response => response.StatusCode == HttpStatusCode.Created);
        Assert.All(created, response => Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict));
        var invitations = new List<CareInvitationDto>();
        foreach (var response in created)
        {
            if (response.StatusCode == HttpStatusCode.Created)
                invitations.Add((await response.Content.ReadFromJsonAsync<CareInvitationDto>())!);
            else await Error(response, 409, "invalidState");
        }
        var id = Assert.Single(invitations.Select(row => row.InvitationId).Distinct());
        Assert.Equal(id, (await Invite(caretaker, senior.Phone)).InvitationId);
        var accepted = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            senior.Client.PostAsync($"/api/v1/care-invitations/{id}/accept", null)));
        Assert.Contains(accepted, response => response.StatusCode == HttpStatusCode.OK);
        Assert.All(accepted, response => Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        var relationships = new List<CareRelationshipDto>();
        foreach (var response in accepted)
        {
            if (response.StatusCode == HttpStatusCode.OK)
                relationships.Add((await response.Content.ReadFromJsonAsync<CareRelationshipDto>())!);
            else await Error(response, 409, "invalidState");
        }
        var relationshipId = Assert.Single(relationships.Select(row => row.RelationshipId).Distinct());
        Assert.Equal(relationshipId, (await Accept(senior, id)).RelationshipId);
        await using var context = CareContext();
        Assert.Equal(1, await context.SeniorCareInvitations.CountAsync(row => row.SeniorId == senior.Id && row.CaretakerId == caretaker.Id));
        Assert.Equal(1, await context.SeniorCareRelationships.CountAsync(row => row.InvitationId == id));
        var removals = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => caretaker.Client.DeleteAsync($"/api/v1/care-relationships/{relationshipId}")));
        Assert.Contains(removals, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.All(removals, response => Assert.True(response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict));
        Assert.Equal(HttpStatusCode.NoContent, (await caretaker.Client.DeleteAsync($"/api/v1/care-relationships/{relationshipId}")).StatusCode);
    }

    [PostgresFact]
    public async Task Concurrent_accept_and_reject_cannot_both_succeed()
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var senior = await Actor(factory, AccountRole.Senior);
        var invitation = await Invite(caretaker, senior.Phone);
        var responses = await Task.WhenAll(senior.Client.PostAsync($"/api/v1/care-invitations/{invitation.InvitationId}/accept", null),
            senior.Client.PostAsync($"/api/v1/care-invitations/{invitation.InvitationId}/reject", null));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
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
            await Accept(senior, (await Invite(caretaker, senior.Phone)).InvitationId);
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
        var invitations = (await caretaker.Client.GetFromJsonAsync<PageDto<CareInvitationDto>>("/api/v1/me/care-invitations?direction=sent&limit=2"))!;
        var invitationPage = (await caretaker.Client.GetFromJsonAsync<PageDto<CareInvitationDto>>("/api/v1/me/care-invitations?direction=sent&page=2&limit=2"))!;
        Assert.Equal(3, invitations.TotalCount);
        Assert.Equal(2, invitations.Items.Count);
        Assert.Single(invitationPage.Items);
        Assert.DoesNotContain(invitationPage.Items[0].InvitationId, invitations.Items.Select(item => item.InvitationId));
        var caretakers = (await first.Client.GetFromJsonAsync<PageDto<CareRelationshipDto>>($"/api/v1/seniors/{first.Id}/caretakers?limit=1"))!;
        Assert.Equal(caretaker.Id, Assert.Single(caretakers.Items).Caretaker.UserId);
        Assert.Equal(1, caretakers.TotalCount);
        Assert.Empty((await first.Client.GetFromJsonAsync<PageDto<CareRelationshipDto>>($"/api/v1/seniors/{first.Id}/caretakers?page=2&limit=1"))!.Items);
    }

    [PostgresTheory]
    [InlineData("/api/v1/me/seniors?page=0")]
    [InlineData("/api/v1/me/seniors?page=-1")]
    [InlineData("/api/v1/me/seniors?page=2147483647")]
    [InlineData("/api/v1/me/seniors?limit=0")]
    [InlineData("/api/v1/me/seniors?limit=101")]
    [InlineData("/api/v1/me/care-invitations?direction=invalid")]
    [InlineData("/api/v1/me/care-invitations?status=invalid")]
    [InlineData("/api/v1/me/care-invitations?status=0")]
    public async Task Invalid_query_parameters_use_shared_validation(string path)
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Error(await caretaker.Client.GetAsync(path), 400, "validationFailed");
    }

    [PostgresFact]
    public async Task Deactivation_denies_access_immediately_and_cleanup_removes_care_data()
    {
        await using var factory = fixture.Factory();
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var senior = await Actor(factory, AccountRole.Senior);
        var invitation = await Invite(caretaker, senior.Phone);
        await Accept(senior, invitation.InvitationId);
        await using (var context = fixture.Context())
        {
            var account = await context.Accounts.SingleAsync(row => row.Id == senior.Id);
            account.Status = AccountStatus.PendingDeletion;
            account.DeletionRequestedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ICareFacade>().CanAccessSeniorAsync(caretaker.Id, senior.Id, CancellationToken.None));
        Assert.Empty((await caretaker.Client.GetFromJsonAsync<PageDto<SeniorSummaryDto>>("/api/v1/me/seniors"))!.Items);
        foreach (var cleanup in scope.ServiceProvider.GetServices<IAccountDataCleanup>())
            await cleanup.DeleteAsync(senior.Id, CancellationToken.None);
        await using var care = CareContext();
        Assert.False(await care.SeniorCareInvitations.AnyAsync(row => row.SeniorId == senior.Id));
        Assert.False(await care.SeniorCareRelationships.AnyAsync(row => row.SeniorId == senior.Id));
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

    private static async Task<CareInvitationDto> Invite(TestActor caretaker, string identifier)
    {
        var response = await caretaker.Client.PostAsJsonAsync("/api/v1/care-invitations", new { recipientIdentifier = identifier });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invitation = (await response.Content.ReadFromJsonAsync<CareInvitationDto>())!;
        Assert.EndsWith($"/api/v1/care-invitations/{invitation.InvitationId}", response.Headers.Location!.ToString());
        return invitation;
    }

    private static async Task<CareRelationshipDto> Accept(TestActor senior, Guid id)
    {
        var response = await senior.Client.PostAsync($"/api/v1/care-invitations/{id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CareRelationshipDto>())!;
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
