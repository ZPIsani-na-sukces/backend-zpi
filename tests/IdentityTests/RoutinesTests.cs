using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Domain;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Modules.Care.Application.Relationships;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Routines.Application.Occurrences;
using Zpi.Modules.Routines.Application.Occurrences.Decision;
using Zpi.Modules.Routines.Application.Scheduling;
using Zpi.Modules.Routines.Application.Tasks;
using Zpi.Modules.Routines.Domain;
using Zpi.Modules.Routines.Infrastructure.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class RoutinesTests(IdentityApiFixture fixture)
{
    private const string Password = "RoutinePass123!";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Theory]
    [InlineData("Europe/Warsaw", "2026-03-29", "02:30:00", "2026-03-29T01:30:00Z")]
    [InlineData("Europe/Warsaw", "2026-10-25", "02:30:00", "2026-10-25T00:30:00Z")]
    [InlineData("Australia/Lord_Howe", "2026-10-04", "02:15:00", "2026-10-03T15:45:00Z")]
    [InlineData("UTC", "2026-10-25", "02:30:00", "2026-10-25T02:30:00Z")]
    public void Local_schedule_handles_nonexistent_and_ambiguous_hours_without_double_dosing(string id, string date, string time, string utc)
    {
        Assert.Equal(DateTimeOffset.Parse(utc), LocalScheduleTime.Resolve(DateOnly.Parse(date), TimeOnly.Parse(time), TimeZoneInfo.FindSystemTimeZoneById(id)));
    }

    [PostgresFact]
    public async Task Creating_a_task_materializes_stable_occurrences_and_returns_author_etag_and_reminders()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var request = Body(clock) with { Title = "  Lek  ", Instructions = "  Po posiłku  " };
        var response = await Send(caretaker.Client, HttpMethod.Post, Tasks(senior.Id), request);
        await Status(response, 201);
        var task = (await response.Content.ReadFromJsonAsync<RoutineTaskDto>())!;
        Assert.Equal("\"1\"", response.Headers.ETag!.Tag);
        Assert.Equal("Lek", task.Title);
        Assert.Equal("Po posiłku", task.Instructions);
        Assert.Equal(caretaker.Id, task.CreatedBy!.UserId);
        Assert.Equal(new[] { 15, 0 }, task.ReminderOffsetsMinutes);
        Assert.Equal(1, task.ScheduleRevision);
        var read = await senior.Client.GetAsync(response.Headers.Location!);
        await Status(read, 200);
        Assert.Equal(response.Headers.ETag, read.Headers.ETag);
        var first = await Occurrences(senior, clock);
        var repeat = await Occurrences(senior, clock);
        Assert.NotEmpty(first.Items);
        Assert.Equal(first.Items.Select(row => row.OccurrenceId), repeat.Items.Select(row => row.OccurrenceId));
        Assert.All(first.Items, row =>
        {
            Assert.Equal(task.TaskId, row.TaskId);
            Assert.Equal(task.Title, row.Title);
            Assert.Equal("pending", row.Status);
            Assert.Equal(new[] { row.ScheduledAt.AddMinutes(-15), row.ScheduledAt }, row.ReminderAt);
        });
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<RoutinesDbContext>().Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Editing_reschedules_only_pending_future_occurrences_and_preserves_completed_snapshots()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var body = Body(clock);
        var task = await Create(caretaker, senior.Id, body);
        var before = await Occurrences(senior, clock);
        var completed = before.Items[0];
        var cancelled = before.Items[1];
        await Status(await Decide(senior, completed, TaskDecision.Confirmed, clock.GetUtcNow()), 200);
        var saved = await Send(caretaker.Client, HttpMethod.Put, Tasks(senior.Id) + "/" + task.TaskId, body with { Title = "Nowa instrukcja" }, "\"1\"");
        await Status(saved, 200);
        var updated = (await saved.Content.ReadFromJsonAsync<RoutineTaskDto>())!;
        Assert.Equal(2, updated.Version);
        Assert.Equal(2, updated.ScheduleRevision);
        var history = await Occurrences(senior, clock);
        var old = Assert.Single(history.Items, row => row.OccurrenceId == completed.OccurrenceId);
        Assert.Equal("confirmed", old.Status);
        Assert.Equal(body.Title, old.Title);
        Assert.Equal("cancelled", Assert.Single(history.Items, row => row.OccurrenceId == cancelled.OccurrenceId).Status);
        Assert.DoesNotContain(history.Items, row => row.ScheduleRevision == 2 && row.ScheduledAt == completed.ScheduledAt);
        Assert.Contains(history.Items, row => row.ScheduleRevision == 2 && row.ScheduledAt == cancelled.ScheduledAt && row.Title == "Nowa instrukcja");
        await Error(await Decide(senior, cancelled, TaskDecision.Confirmed, clock.GetUtcNow()), 409, "occurrenceCancelled");
    }

    [PostgresFact]
    public async Task Unchanged_definition_and_reordered_schedule_increase_only_the_resource_version()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var body = Body(clock);
        var task = await Create(caretaker, senior.Id, body);
        var before = await Occurrences(senior, clock);
        var reordered = body with { Schedule = body.Schedule with { DaysOfWeek = body.Schedule.DaysOfWeek.Reverse().ToArray(), Times = body.Schedule.Times.Reverse().ToArray() } };
        var response = await Send(caretaker.Client, HttpMethod.Put, Tasks(senior.Id) + "/" + task.TaskId, reordered, "\"1\"");
        await Status(response, 200);
        var updated = (await response.Content.ReadFromJsonAsync<RoutineTaskDto>())!;
        Assert.Equal(2, updated.Version);
        Assert.Equal(1, updated.ScheduleRevision);
        Assert.Equal(before.Items.Select(row => row.OccurrenceId), (await Occurrences(senior, clock)).Items.Select(row => row.OccurrenceId));
    }

    [PostgresFact]
    public async Task Disabling_reactivating_and_deleting_a_task_preserve_its_history()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var body = Body(clock);
        var task = await Create(caretaker, senior.Id, body);
        var first = (await Occurrences(senior, clock)).Items[0];
        await Status(await Send(caretaker.Client, HttpMethod.Put, Tasks(senior.Id) + "/" + task.TaskId, body with { IsActive = false }, "\"1\""), 200);
        Assert.All((await Occurrences(senior, clock)).Items, row => Assert.Equal("cancelled", row.Status));
        await Status(await Send(caretaker.Client, HttpMethod.Put, Tasks(senior.Id) + "/" + task.TaskId, body, "\"2\""), 200);
        var reactivated = await Occurrences(senior, clock, "status=pending");
        Assert.NotEmpty(reactivated.Items);
        Assert.All(reactivated.Items, row => Assert.Equal(3, row.ScheduleRevision));
        await Status(await Send(caretaker.Client, HttpMethod.Delete, Tasks(senior.Id) + "/" + task.TaskId, etag: "\"3\""), 204);
        await Error(await senior.Client.GetAsync(Tasks(senior.Id) + "/" + task.TaskId), 404, "resourceNotFound");
        Assert.Empty((await senior.Client.GetFromJsonAsync<CursorPageDto<RoutineTaskDto>>(Tasks(senior.Id)))!.Items);
        await Status(await senior.Client.GetAsync(OccurrencePath(senior.Id, first.OccurrenceId)), 200);
        await Error(await Send(caretaker.Client, HttpMethod.Delete, Tasks(senior.Id) + "/" + task.TaskId, etag: "\"4\""), 404, "resourceNotFound");
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
        var stored = await context.RoutineTasks.SingleAsync(row => row.Id == task.TaskId);
        Assert.Equal(caretaker.Id, stored.DeletedByAccountId);
        Assert.Equal(4, stored.Version);
        Assert.NotNull(stored.DeletedAt);
        Assert.False(stored.IsActive);
    }

    [PostgresFact]
    public async Task Missed_occurrences_can_be_confirmed_late_and_decisions_are_immutable_and_idempotent()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await Create(caretaker, senior.Id, Body(clock));
        var first = (await Occurrences(senior, clock)).Items[0];
        var performed = first.ScheduledAt.AddMinutes(1);
        clock.Advance(TimeSpan.FromDays(3));
        var missed = await senior.Client.GetFromJsonAsync<TaskOccurrenceDto>(OccurrencePath(senior.Id, first.OccurrenceId));
        Assert.Equal("missed", missed!.Status);
        var from = first.ScheduledAt.AddHours(-1);
        var to = first.ScheduledAt.AddHours(1);
        var path = OccurrenceRange(senior.Id, from, to) + "&status=missed";
        Assert.Equal(first.OccurrenceId, Assert.Single((await senior.Client.GetFromJsonAsync<CursorPageDto<TaskOccurrenceDto>>(path))!.Items).OccurrenceId);
        var response = await Decide(senior, first, TaskDecision.Confirmed, performed);
        await Status(response, 200);
        var confirmed = (await response.Content.ReadFromJsonAsync<TaskOccurrenceDto>())!;
        Assert.Equal("confirmed", confirmed.Status);
        Assert.Equal(performed, confirmed.Decision!.PerformedAt);
        Assert.Equal(clock.GetUtcNow(), confirmed.Decision.ReceivedAt);
        var repeat = await Decide(senior, first, TaskDecision.Confirmed, clock.GetUtcNow());
        await Status(repeat, 200);
        Assert.Equal(confirmed.Decision, (await repeat.Content.ReadFromJsonAsync<TaskOccurrenceDto>())!.Decision);
        await Error(await Decide(senior, first, TaskDecision.Skipped, clock.GetUtcNow()), 409, "decisionAlreadyRecorded");
        Assert.Empty((await senior.Client.GetFromJsonAsync<CursorPageDto<TaskOccurrenceDto>>(path))!.Items);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<RoutinesDbContext>().TaskConfirmations.CountAsync(row => row.TaskOccurrenceId == first.OccurrenceId));
    }

    [PostgresFact]
    public async Task Past_pending_occurrences_keep_their_original_snapshot_when_a_task_is_edited_or_deleted()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var body = Body(clock);
        var task = await Create(caretaker, senior.Id, body);
        var old = (await Occurrences(senior, clock)).Items[0];
        clock.Advance(TimeSpan.FromDays(2));
        await Status(await Send(caretaker.Client, HttpMethod.Put, Tasks(senior.Id) + "/" + task.TaskId, body with { Title = "Nowy lek" }, "\"1\""), 200);
        var read = (await senior.Client.GetFromJsonAsync<TaskOccurrenceDto>(OccurrencePath(senior.Id, old.OccurrenceId)))!;
        Assert.Equal(body.Title, read.Title);
        Assert.NotEqual("cancelled", read.Status);
        await Status(await Send(caretaker.Client, HttpMethod.Delete, Tasks(senior.Id) + "/" + task.TaskId, etag: "\"2\""), 204);
        await Status(await Decide(senior, old, TaskDecision.Skipped, clock.GetUtcNow()), 200);
        Assert.Equal("skipped", (await caretaker.Client.GetFromJsonAsync<TaskOccurrenceDto>(OccurrencePath(senior.Id, old.OccurrenceId)))!.Status);
    }

    [PostgresFact]
    public async Task Worker_extends_the_horizon_and_repeated_generation_keeps_existing_identifiers()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var task = await Create(caretaker, senior.Id, Body(clock));
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
        var before = await context.TaskOccurrences.Where(row => row.RoutineTaskId == task.TaskId).Select(row => row.Id).ToArrayAsync();
        clock.Advance(TimeSpan.FromDays(15));
        await scope.ServiceProvider.GetRequiredService<OccurrenceProcessor>().ProcessAsync(CancellationToken.None);
        var after = await context.TaskOccurrences.Where(row => row.RoutineTaskId == task.TaskId).Select(row => row.Id).ToArrayAsync();
        Assert.True(after.Length > before.Length);
        Assert.All(before, id => Assert.Contains(id, after));
        await scope.ServiceProvider.GetRequiredService<OccurrenceProcessor>().ProcessAsync(CancellationToken.None);
        Assert.Equal(after.Length, await context.TaskOccurrences.CountAsync(row => row.RoutineTaskId == task.TaskId));
        var occurrences = await Occurrences(senior, clock);
        Assert.NotEmpty(occurrences.Items);
        Assert.All(occurrences.Items, row => Assert.True(row.ScheduledAt < clock.GetUtcNow().AddDays(30)));
    }

    [PostgresFact]
    public async Task Dst_colliding_times_materialize_one_occurrence_with_a_stable_id()
    {
        await using var factory = fixture.Factory();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
        var from = DateTimeOffset.Parse("2026-03-29T00:00:00Z");
        var task = new RoutineTask { SeniorId = Guid.CreateVersion7(), CreatedByAccountId = Guid.CreateVersion7(),
            Title = "Lek", Instructions = "Jedna tabletka", CreatedAt = from.AddDays(-1), UpdatedAt = from,
            StartsOn = new DateOnly(2026, 3, 29), EndsOn = new DateOnly(2026, 3, 29) };
        var rows = new RoutineScheduleInputDto("Europe/Warsaw", [7], [new(2, 30), new(3, 30)]).CreateRows(task.Id, 1);
        context.RoutineTasks.Add(task);
        context.RoutineTaskSchedules.AddRange(rows);
        var generator = scope.ServiceProvider.GetRequiredService<OccurrenceGenerator>();
        await generator.GenerateAsync(task, rows, from, from.AddDays(1), from, CancellationToken.None);
        await context.SaveChangesAsync();
        var occurrence = await context.TaskOccurrences.SingleAsync(row => row.RoutineTaskId == task.Id);
        Assert.Equal(DateTimeOffset.Parse("2026-03-29T01:30:00Z"), occurrence.ScheduledAt);
        await generator.GenerateAsync(task, rows, from, from.AddDays(1), from, CancellationToken.None);
        await context.SaveChangesAsync();
        Assert.Equal(occurrence.Id, (await context.TaskOccurrences.SingleAsync(row => row.RoutineTaskId == task.Id)).Id);
    }

    [PostgresFact]
    public async Task Roles_and_current_care_relationship_control_all_task_and_occurrence_access()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var otherSenior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var outsider = await Actor(factory, AccountRole.Caretaker);
        var relationship = await Link(senior, caretaker);
        var task = await Create(caretaker, senior.Id, Body(clock));
        var occurrence = (await Occurrences(senior, clock)).Items[0];
        foreach (var actor in new[] { otherSenior, outsider })
        {
            await Error(await actor.Client.GetAsync(Tasks(senior.Id)), 404, "resourceNotFound");
            await Error(await actor.Client.GetAsync(Tasks(senior.Id) + "/" + task.TaskId), 404, "resourceNotFound");
            await Error(await actor.Client.GetAsync(OccurrencePath(senior.Id, occurrence.OccurrenceId)), 404, "resourceNotFound");
            await Error(await actor.Client.GetAsync(OccurrenceRange(senior.Id, clock.GetUtcNow(), clock.GetUtcNow().AddDays(2))), 404, "resourceNotFound");
        }
        await Error(await Send(senior.Client, HttpMethod.Post, Tasks(senior.Id), Body(clock)), 403, "operationForbidden");
        await Error(await Send(outsider.Client, HttpMethod.Post, Tasks(senior.Id), Body(clock)), 404, "resourceNotFound");
        await Error(await Decide(caretaker, occurrence, TaskDecision.Confirmed, clock.GetUtcNow()), 403, "operationForbidden");
        await Error(await Decide(otherSenior, occurrence, TaskDecision.Confirmed, clock.GetUtcNow()), 404, "resourceNotFound");
        await Error(await caretaker.Client.GetAsync(Tasks(otherSenior.Id) + "/" + task.TaskId), 404, "resourceNotFound");
        await Status(await caretaker.Client.DeleteAsync($"/api/v1/care-relationships/{relationship.RelationshipId}"), 204);
        await Error(await caretaker.Client.GetAsync(Tasks(senior.Id)), 404, "resourceNotFound");
        await Error(await Send(caretaker.Client, HttpMethod.Put, Tasks(senior.Id) + "/" + task.TaskId, Body(clock), "\"1\""), 404, "resourceNotFound");
        await using var identity = fixture.Context();
        var account = await identity.Accounts.SingleAsync(row => row.Id == senior.Id);
        account.Status = AccountStatus.PendingDeletion;
        await identity.SaveChangesAsync();
        await Error(await senior.Client.GetAsync(Tasks(senior.Id)), 401, "unauthenticated");
    }

    [PostgresFact]
    public async Task Updates_and_deletes_require_etag_and_concurrent_updates_do_not_overwrite_each_other()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var body = Body(clock);
        var task = await Create(caretaker, senior.Id, body);
        var path = Tasks(senior.Id) + "/" + task.TaskId;
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete })
        {
            await Error(await Send(caretaker.Client, method, path, method == HttpMethod.Put ? body : null), 428, "preconditionRequired");
            await Error(await Send(caretaker.Client, method, path, method == HttpMethod.Put ? body : null, "W/\"1\""), 400, "validationFailed");
            await Error(await Send(caretaker.Client, method, path, method == HttpMethod.Put ? body : null, "\"2\""), 412, "versionMismatch");
        }
        var responses = await Task.WhenAll(Send(caretaker.Client, HttpMethod.Put, path, body with { Title = "First" }, "\"1\""),
            Send(caretaker.Client, HttpMethod.Put, path, body with { Title = "Second" }, "\"1\""));
        var success = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed);
        Assert.Equal(2, (await success.Content.ReadFromJsonAsync<RoutineTaskDto>())!.Version);
        Assert.Equal(2, (await senior.Client.GetFromJsonAsync<RoutineTaskDto>(path))!.Version);
    }

    [PostgresFact]
    public async Task Concurrent_decisions_record_only_one_choice_and_can_be_retried()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await Create(caretaker, senior.Id, Body(clock));
        var occurrence = (await Occurrences(senior, clock)).Items[0];
        var responses = await Task.WhenAll(Decide(senior, occurrence, TaskDecision.Confirmed, clock.GetUtcNow()),
            Decide(senior, occurrence, TaskDecision.Skipped, clock.GetUtcNow()));
        var success = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var saved = (await success.Content.ReadFromJsonAsync<TaskOccurrenceDto>())!;
        var decision = Enum.Parse<TaskDecision>(saved.Decision!.Decision, true);
        await Status(await Decide(senior, occurrence, decision, clock.GetUtcNow()), 200);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<RoutinesDbContext>().TaskConfirmations.CountAsync(row => row.TaskOccurrenceId == occurrence.OccurrenceId));
    }

    [PostgresFact]
    public async Task Validation_rejects_invalid_schedules_ranges_and_decision_timestamps()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var valid = Body(clock);
        var invalid = new[]
        {
            valid with { Title = " " }, valid with { Title = new string('a', 151) }, valid with { Instructions = "" },
            valid with { Type = (RoutineType)99 }, valid with { StartsOn = default }, valid with { EndsOn = valid.StartsOn.AddDays(-1) },
            valid with { Schedule = null! }, valid with { Schedule = valid.Schedule with { TimeZone = "not/a-zone" } },
            valid with { Schedule = valid.Schedule with { DaysOfWeek = [] } }, valid with { Schedule = valid.Schedule with { DaysOfWeek = [1, 1] } },
            valid with { Schedule = valid.Schedule with { DaysOfWeek = [0] } }, valid with { Schedule = valid.Schedule with { DaysOfWeek = [8] } },
            valid with { Schedule = valid.Schedule with { Times = [] } }, valid with { Schedule = valid.Schedule with { Times = [new(8, 0), new(8, 0)] } },
            valid with { Schedule = valid.Schedule with { Times = Enumerable.Range(0, 13).Select(hour => new TimeOnly(hour, 0)).ToArray() } },
            valid with { Schedule = valid.Schedule with { Times = [new TimeOnly(8, 0).Add(TimeSpan.FromMilliseconds(1))] } }
        };
        foreach (var body in invalid) await Error(await Send(caretaker.Client, HttpMethod.Post, Tasks(senior.Id), body), 400, "validationFailed");
        var task = await Create(caretaker, senior.Id, valid);
        var occurrence = (await Occurrences(senior, clock)).Items[0];
        foreach (var at in new[] { clock.GetUtcNow().AddDays(-31), clock.GetUtcNow().AddMinutes(6) })
            await Error(await Decide(senior, occurrence, TaskDecision.Confirmed, at), 400, "validationFailed");
        var range = OccurrenceRange(senior.Id, clock.GetUtcNow(), clock.GetUtcNow().AddDays(2));
        foreach (var query in new[] { "cursor=invalid", "limit=101", "type=other", "status=other", "taskId=" + Guid.Empty })
            await Error(await senior.Client.GetAsync(range + "&" + query), 400, "validationFailed");
        await Error(await senior.Client.GetAsync($"/api/v1/seniors/{senior.Id}/task-occurrences"), 400, "validationFailed");
        await Error(await senior.Client.GetAsync(OccurrenceRange(senior.Id, clock.GetUtcNow(), clock.GetUtcNow().AddDays(31))), 400, "validationFailed");
        await Error(await senior.Client.GetAsync(OccurrenceRange(senior.Id, clock.GetUtcNow(), clock.GetUtcNow().AddDays(-1))), 400, "validationFailed");
        Assert.Single((await senior.Client.GetFromJsonAsync<CursorPageDto<RoutineTaskDto>>(Tasks(senior.Id)))!.Items);
        Assert.Equal(1, (await senior.Client.GetFromJsonAsync<RoutineTaskDto>(Tasks(senior.Id) + "/" + task.TaskId))!.Version);
    }

    [PostgresFact]
    public async Task Task_and_occurrence_lists_filter_and_paginate_in_the_database()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var body = Body(clock);
        var first = await Create(caretaker, senior.Id, body with { Title = "Anna" });
        var second = await Create(caretaker, senior.Id, body with { Title = "Anna" });
        var inactive = await Create(caretaker, senior.Id, body with { Title = "Woda", Type = RoutineType.Hydration, IsActive = false });
        var initial = (await senior.Client.GetFromJsonAsync<CursorPageDto<RoutineTaskDto>>(Tasks(senior.Id) + "?limit=1"))!;
        Assert.NotNull(initial.NextCursor);
        var page = (await senior.Client.GetFromJsonAsync<CursorPageDto<RoutineTaskDto>>(Tasks(senior.Id) + "?cursor=" + initial.NextCursor + "&limit=1"))!;
        Assert.Equal(second.TaskId, Assert.Single(page.Items).TaskId);
        Assert.Equal(inactive.TaskId, Assert.Single((await senior.Client.GetFromJsonAsync<CursorPageDto<RoutineTaskDto>>(Tasks(senior.Id) + "?type=hydration&search=WODA&isActive=false"))!.Items).TaskId);
        var initialOccurrences = await Occurrences(senior, clock, "taskId=" + first.TaskId + "&limit=1");
        var occurrences = await Occurrences(senior, clock, "taskId=" + first.TaskId + "&cursor=" + initialOccurrences.NextCursor + "&limit=1");
        var full = await Occurrences(senior, clock, "taskId=" + first.TaskId);
        Assert.NotNull(initialOccurrences.NextCursor);
        Assert.Equal(full.Items[1].OccurrenceId, Assert.Single(occurrences.Items).OccurrenceId);
        var offsetRange = OccurrenceRange(senior.Id, clock.GetUtcNow().ToOffset(TimeSpan.FromHours(2)),
            clock.GetUtcNow().AddDays(2).ToOffset(TimeSpan.FromHours(2))) + "&taskId=" + first.TaskId;
        var withOffset = (await senior.Client.GetFromJsonAsync<CursorPageDto<TaskOccurrenceDto>>(offsetRange))!;
        Assert.Equal(full.Items.Select(row => row.OccurrenceId), withOffset.Items.Select(row => row.OccurrenceId));
        Assert.Empty((await Occurrences(senior, clock, "type=hydration")).Items);
        Assert.All((await Occurrences(senior, clock, "taskId=" + first.TaskId)).Items, row => Assert.Equal(first.TaskId, row.TaskId));
        foreach (var query in new[] { "cursor=invalid", "limit=101", "type=other", "search=" + new string('a', 101) })
            await Error(await senior.Client.GetAsync(Tasks(senior.Id) + "?" + query), 400, "validationFailed");
    }

    [PostgresFact]
    public async Task Active_task_limit_applies_to_creation_and_reactivation_and_is_safe_for_concurrent_creates()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var body = Body(clock);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
            for (var index = 0; index < 49; index++)
            {
                var task = new RoutineTask { SeniorId = senior.Id, CreatedByAccountId = caretaker.Id, Title = "Existing", Instructions = "One", CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
                body.ApplyTo(task);
                context.RoutineTasks.Add(task);
                context.RoutineTaskSchedules.AddRange(body.Schedule.CreateRows(task.Id, 1));
            }
            await context.SaveChangesAsync();
        }
        var responses = await Task.WhenAll(Send(caretaker.Client, HttpMethod.Post, Tasks(senior.Id), body), Send(caretaker.Client, HttpMethod.Post, Tasks(senior.Id), body));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await Error(await Send(caretaker.Client, HttpMethod.Post, Tasks(senior.Id), body), 409, "taskLimitReached");
        var inactive = await Create(caretaker, senior.Id, body with { IsActive = false });
        await Error(await Send(caretaker.Client, HttpMethod.Put, Tasks(senior.Id) + "/" + inactive.TaskId, body, "\"1\""), 409, "taskLimitReached");
        var active = (await responses.Single(response => response.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<RoutineTaskDto>())!;
        await Status(await Send(caretaker.Client, HttpMethod.Delete, Tasks(senior.Id) + "/" + active.TaskId, etag: "\"1\""), 204);
        await Status(await Send(caretaker.Client, HttpMethod.Put, Tasks(senior.Id) + "/" + inactive.TaskId, body, "\"1\""), 200);
    }

    [PostgresFact]
    public async Task Account_cleanup_removes_owned_tasks_schedules_occurrences_and_decisions()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var task = await Create(caretaker, senior.Id, Body(clock));
        var occurrence = (await Occurrences(senior, clock)).Items[0];
        await Status(await Decide(senior, occurrence, TaskDecision.Skipped, clock.GetUtcNow()), 200);
        await using var scope = factory.Services.CreateAsyncScope();
        foreach (var cleanup in scope.ServiceProvider.GetServices<IAccountDataCleanup>()) await cleanup.DeleteAsync(senior.Id, CancellationToken.None);
        var context = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
        Assert.False(await context.RoutineTasks.AnyAsync(row => row.Id == task.TaskId));
        Assert.False(await context.RoutineTaskSchedules.AnyAsync(row => row.RoutineTaskId == task.TaskId));
        Assert.False(await context.TaskOccurrences.AnyAsync(row => row.SeniorId == senior.Id));
        Assert.False(await context.TaskConfirmations.AnyAsync(row => row.TaskOccurrenceId == occurrence.OccurrenceId));
    }

    [PostgresFact]
    public async Task Anonymous_users_cannot_access_any_routines_endpoint()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var client = factory.CreateClient();
        var id = Guid.CreateVersion7();
        var path = Tasks(id);
        await Error(await client.GetAsync(path), 401, "unauthenticated");
        await Error(await client.GetAsync(path + "/" + id), 401, "unauthenticated");
        await Error(await Send(client, HttpMethod.Post, path, Body(clock)), 401, "unauthenticated");
        await Error(await Send(client, HttpMethod.Put, path + "/" + id, Body(clock), "\"1\""), 401, "unauthenticated");
        await Error(await Send(client, HttpMethod.Delete, path + "/" + id, etag: "\"1\""), 401, "unauthenticated");
        await Error(await client.GetAsync(OccurrenceRange(id, clock.GetUtcNow(), clock.GetUtcNow().AddDays(1))), 401, "unauthenticated");
        await Error(await client.GetAsync(OccurrencePath(id, id)), 401, "unauthenticated");
        await Error(await Send(client, HttpMethod.Put, OccurrencePath(id, id) + "/decision", new RecordTaskDecisionRequest(TaskDecision.Confirmed, clock.GetUtcNow())), 401, "unauthenticated");
    }

    private WebApplicationFactory<Program> Factory(TestClock clock) => fixture.Factory().WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
    private async Task<TestActor> Actor(WebApplicationFactory<Program> factory, AccountRole role)
    {
        var phone = "+48" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
        await using var context = fixture.Context();
        var now = DateTimeOffset.UtcNow;
        var account = new Account { RoleId = await context.Roles.Where(row => row.Code == role).Select(row => row.Id).SingleAsync(),
            PhoneNumber = phone, NormalizedPhoneNumber = phone, PasswordHash = "", FirstName = "Jan", LastName = "Kowalski",
            Status = AccountStatus.Active, PhoneVerifiedAt = now, CreatedAt = now, UpdatedAt = now };
        account.PasswordHash = factory.Services.GetRequiredService<PasswordHashing>().Hash(account, Password);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" });
        await Status(response, 200);
        var session = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new TestActor(account.Id, phone, client);
    }
    private static async Task<CareRelationshipDto> Link(TestActor senior, TestActor caretaker)
    {
        var created = await caretaker.Client.PostAsync("/api/v1/care-invitations", null);
        await Status(created, 201);
        var invitation = (await created.Content.ReadFromJsonAsync<CareInvitationDto>())!;
        var accepted = await senior.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = invitation.Code });
        await Status(accepted, 200);
        return (await accepted.Content.ReadFromJsonAsync<CareRelationshipDto>())!;
    }
    private static SaveRoutineTaskRequest Body(TestClock clock) => new(RoutineType.Medication, "Lek", "Jedna tabletka",
        DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1), null, true,
        new("UTC", [1, 2, 3, 4, 5, 6, 7], [new(8, 0), new(20, 0)]));
    private static string Tasks(Guid id) => $"/api/v1/seniors/{id}/routine-tasks";
    private static string OccurrencePath(Guid seniorId, Guid occurrenceId) => $"/api/v1/seniors/{seniorId}/task-occurrences/{occurrenceId}";
    private static string OccurrenceRange(Guid seniorId, DateTimeOffset from, DateTimeOffset to) =>
        $"/api/v1/seniors/{seniorId}/task-occurrences?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
    private static async Task<CursorPageDto<TaskOccurrenceDto>> Occurrences(TestActor senior, TestClock clock, string? query = null) =>
        (await senior.Client.GetFromJsonAsync<CursorPageDto<TaskOccurrenceDto>>(OccurrenceRange(senior.Id, clock.GetUtcNow(), clock.GetUtcNow().AddDays(2)) + (query is null ? "" : "&" + query)))!;
    private static Task<HttpResponseMessage> Decide(TestActor actor, TaskOccurrenceDto occurrence, TaskDecision decision, DateTimeOffset performed) =>
        Send(actor.Client, HttpMethod.Put, OccurrencePath(occurrence.SeniorId, occurrence.OccurrenceId) + "/decision", new RecordTaskDecisionRequest(decision, performed));
    private static async Task<RoutineTaskDto> Create(TestActor actor, Guid seniorId, SaveRoutineTaskRequest body)
    {
        var response = await Send(actor.Client, HttpMethod.Post, Tasks(seniorId), body);
        await Status(response, 201);
        return (await response.Content.ReadFromJsonAsync<RoutineTaskDto>())!;
    }
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body = null, string? etag = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        return client.SendAsync(request);
    }
    private static async Task Status(HttpResponseMessage response, int status) =>
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    private static async Task Error(HttpResponseMessage response, int status, string code)
    {
        await Status(response, status);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan delta) => now += delta;
    }
    private sealed record TestActor(Guid Id, string Phone, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
