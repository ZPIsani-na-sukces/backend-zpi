using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;
using Zpi.Modules.MedicalRecords.Domain;
using Zpi.Modules.MedicalRecords.Persistence;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;
using Zpi.Modules.Routines.Application.Scheduling;
using Zpi.Modules.Routines.Domain;
using Zpi.Modules.Routines.Infrastructure.Persistence;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;
using Zpi.Modules.SeniorProfile.Domain;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Api;

internal sealed class DevelopmentDatabaseMigrationService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        DbContext[] contexts =
        [
            services.GetRequiredService<IdentityDbContext>(),
            services.GetRequiredService<CareDbContext>(),
            services.GetRequiredService<SeniorProfileDbContext>(),
            services.GetRequiredService<SafetyDbContext>(),
            services.GetRequiredService<RoutinesDbContext>(),
            services.GetRequiredService<MedicalRecordsDbContext>(),
            services.GetRequiredService<NotificationsDbContext>()
        ];

        foreach (var context in contexts)
            await context.Database.MigrateAsync(cancellationToken);

        await SeedDevelopmentDataAsync(services, cancellationToken);
    }

    private static async Task SeedDevelopmentDataAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var identityContext = services.GetRequiredService<IdentityDbContext>();
        var careContext = services.GetRequiredService<CareDbContext>();
        var seniorProfileContext = services.GetRequiredService<SeniorProfileDbContext>();
        var safetyContext = services.GetRequiredService<SafetyDbContext>();
        var routinesContext = services.GetRequiredService<RoutinesDbContext>();
        var medicalContext = services.GetRequiredService<MedicalRecordsDbContext>();
        var notificationsContext = services.GetRequiredService<NotificationsDbContext>();
        var passwords = services.GetRequiredService<PasswordHashing>();
        var clock = services.GetService<TimeProvider>() ?? TimeProvider.System;
        var now = clock.GetUtcNow();

        var seniorId = Guid.Parse("019a0000-0000-7000-8000-000000000010");
        var secondSeniorId = Guid.Parse("019a0000-0000-7000-8000-000000000011");
        var unpairedSeniorId = Guid.Parse("019a0000-0000-7000-8000-000000000012");
        var caretakerId = Guid.Parse("019a0000-0000-7000-8000-000000000020");
        var relationshipId = Guid.Parse("019a0000-0000-7000-8000-000000000040");
        var secondRelationshipId = Guid.Parse("019a0000-0000-7000-8000-000000000041");
        var unpairedInvitationId = Guid.Parse("019a0000-0000-7000-8000-000000000042");
        var deviceId = Guid.Parse("019a0000-0000-7000-8000-000000000050");
        var deviceInstallationId = Guid.Parse("019a0000-0000-7000-8000-000000000051");
        var locationSampleId = Guid.Parse("019a0000-0000-7000-8000-000000000060");
        var batterySampleId = Guid.Parse("019a0000-0000-7000-8000-000000000061");
        var homeAddressId = Guid.Parse("019a0000-0000-7000-8000-000000000070");
        var secondHomeAddressId = Guid.Parse("019a0000-0000-7000-8000-000000000071");
        var geoZoneId = Guid.Parse("019a0000-0000-7000-8000-000000000075");
        var firstContactId = Guid.Parse("019a0000-0000-7000-8000-000000000080");
        var secondContactId = Guid.Parse("019a0000-0000-7000-8000-000000000081");
        var sosIncidentId = Guid.Parse("019a0000-0000-7000-8000-000000000090");
        var lowBatteryIncidentId = Guid.Parse("019a0000-0000-7000-8000-000000000091");
        var sosNotificationId = Guid.Parse("019a0000-0000-7000-8000-0000000000a0");
        var sosDeliveryId = Guid.Parse("019a0000-0000-7000-8000-0000000000a1");
        var lowBatteryNotificationId = Guid.Parse("019a0000-0000-7000-8000-0000000000a2");
        var lowBatteryDeliveryId = Guid.Parse("019a0000-0000-7000-8000-0000000000a3");

        var seniorRole = await identityContext.Roles.SingleOrDefaultAsync(r => r.Code == AccountRole.Senior, cancellationToken);
        var caretakerRole = await identityContext.Roles.SingleOrDefaultAsync(r => r.Code == AccountRole.Caretaker, cancellationToken);

        if (seniorRole is null || caretakerRole is null) return;

        var senior = await EnsureAccountAsync(
            identityContext,
            passwords,
            seniorId,
            seniorRole.Id,
            "senior@example.com",
            "+48111222333",
            "Jan",
            "Kowalski",
            now,
            cancellationToken);

        var secondSenior = await EnsureAccountAsync(
            identityContext,
            passwords,
            secondSeniorId,
            seniorRole.Id,
            "senior2@example.com",
            "+48222333444",
            "Maria",
            "Wiśniewska",
            now,
            cancellationToken);

        var unpairedSenior = await EnsureAccountAsync(
            identityContext,
            passwords,
            unpairedSeniorId,
            seniorRole.Id,
            "senior3@example.com",
            "+48333444555",
            "Marek",
            "Zieliński",
            now,
            cancellationToken);

        var caretaker = await EnsureAccountAsync(
            identityContext,
            passwords,
            caretakerId,
            caretakerRole.Id,
            "opiekun@example.com",
            "+48999888777",
            "Anna",
            "Nowak",
            now,
            cancellationToken);

        await EnsureRelationshipAsync(careContext, relationshipId, senior.Id, caretaker.Id, now, cancellationToken);
        await EnsureRelationshipAsync(careContext, secondRelationshipId, secondSenior.Id, caretaker.Id, now, cancellationToken);

        var pendingInvitationExists = await careContext.SeniorCareInvitations.AnyAsync(
            i => (i.SenderAccountId == unpairedSenior.Id && i.Status == InvitationStatus.Pending) || i.Code == "654321",
            cancellationToken);
        if (!pendingInvitationExists)
        {
            careContext.SeniorCareInvitations.Add(new SeniorCareInvitation
            {
                Id = unpairedInvitationId,
                SenderAccountId = unpairedSenior.Id,
                Code = "654321",
                Status = InvitationStatus.Pending,
                CreatedAt = now,
                ExpiresAt = now.AddDays(7)
            });
            await careContext.SaveChangesAsync(cancellationToken);
        }

        var device = await safetyContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId || d.AssignedAccountId == senior.Id, cancellationToken);
        if (device is null)
        {
            device = new Device
            {
                Id = deviceId,
                AssignedAccountId = senior.Id,
                InstallationId = deviceInstallationId,
                Platform = DevicePlatform.Android,
                AppVersion = "1.0.0",
                MonitoringEnabled = true,
                RegisteredAt = now,
                LastOnlineAt = now
            };
            safetyContext.Devices.Add(device);
            await safetyContext.SaveChangesAsync(cancellationToken);
        }

        var locationSampleExists = await safetyContext.LocationSamples.AnyAsync(l => l.Id == locationSampleId || l.DeviceId == device.Id, cancellationToken);
        if (!locationSampleExists)
        {
            safetyContext.LocationSamples.Add(new LocationSample
            {
                Id = locationSampleId,
                DeviceId = device.Id,
                Latitude = 51.107883,
                Longitude = 17.038538,
                AccuracyMeters = 5.0,
                SpeedMps = 0.0,
                CapturedAt = now,
                ReceivedAt = now
            });
            await safetyContext.SaveChangesAsync(cancellationToken);
        }

        var batterySample = await safetyContext.BatterySamples
            .OrderByDescending(b => b.CapturedAt)
            .FirstOrDefaultAsync(b => b.Id == batterySampleId || b.DeviceId == device.Id, cancellationToken);
        if (batterySample is null)
        {
            safetyContext.BatterySamples.Add(new BatterySample
            {
                Id = batterySampleId,
                DeviceId = device.Id,
                BatteryLevelPercent = 15,
                IsCharging = false,
                CapturedAt = now,
                ReceivedAt = now
            });
            await safetyContext.SaveChangesAsync(cancellationToken);
        }
        else if (batterySample.BatteryLevelPercent != 15 || batterySample.IsCharging)
        {
            batterySample.BatteryLevelPercent = 15;
            batterySample.IsCharging = false;
            batterySample.CapturedAt = now;
            batterySample.ReceivedAt = now;
            await safetyContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureHomeAddressAsync(
            safetyContext,
            homeAddressId,
            senior.Id,
            "50-001",
            "Wrocław",
            "Rynek",
            "1",
            "10",
            51.107883,
            17.038538,
            now,
            cancellationToken);

        await EnsureHomeAddressAsync(
            safetyContext,
            secondHomeAddressId,
            secondSenior.Id,
            "31-019",
            "Kraków",
            "Floriańska",
            "15",
            "3",
            50.06465,
            19.94498,
            now,
            cancellationToken);

        var geoZoneExists = await safetyContext.GeoZones.AnyAsync(
            g => g.Id == geoZoneId || (g.SeniorId == senior.Id && g.DeletedAt == null),
            cancellationToken);
        if (!geoZoneExists)
        {
            safetyContext.GeoZones.Add(new GeoZone
            {
                Id = geoZoneId,
                SeniorId = senior.Id,
                Name = "Dom i okolica",
                CenterLatitude = 51.107883,
                CenterLongitude = 17.038538,
                RadiusMeters = 300,
                IsActive = true,
                SmsOnExitEnabled = true,
                CreatedByAccountId = caretaker.Id,
                CreatedAt = now,
                UpdatedAt = now
            });
            await safetyContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureContactAsync(
            seniorProfileContext,
            firstContactId,
            senior.Id,
            caretaker.Id,
            "Piotr Kowalski",
            "Piotr",
            "Kowalski",
            "Syn",
            "+48600700800",
            now,
            cancellationToken);

        await EnsureContactAsync(
            seniorProfileContext,
            secondContactId,
            secondSenior.Id,
            caretaker.Id,
            "Katarzyna Wiśniewska",
            "Katarzyna",
            "Wiśniewska",
            "Córka",
            "+48500600700",
            now,
            cancellationToken);

        var activeSosIncident = await safetyContext.SosIncidents.FirstOrDefaultAsync(
            i => i.SeniorId == senior.Id && i.Status != IncidentStatus.Resolved,
            cancellationToken);
        if (activeSosIncident is null)
        {
            var existingSeededSos = await safetyContext.SosIncidents.FirstOrDefaultAsync(i => i.Id == sosIncidentId, cancellationToken);
            if (existingSeededSos is not null)
            {
                existingSeededSos.Status = IncidentStatus.Open;
                existingSeededSos.AcknowledgedAt = null;
                existingSeededSos.AcknowledgedByAccountId = null;
                existingSeededSos.ResolvedAt = null;
                existingSeededSos.ResolvedByAccountId = null;
                existingSeededSos.ResolutionNote = null;
                existingSeededSos.OccurredAt = now;
                existingSeededSos.ReceivedAt = now;
                existingSeededSos.Version += 1;
                activeSosIncident = existingSeededSos;
            }
            else
            {
                activeSosIncident = new SosIncident
                {
                    Id = sosIncidentId,
                    SeniorId = senior.Id,
                    DeviceId = device.Id,
                    Status = IncidentStatus.Open,
                    OccurredAt = now,
                    ReceivedAt = now,
                    Latitude = 51.107883,
                    Longitude = 17.038538,
                    AccuracyMeters = 5.0,
                    SpeedMps = 0.0,
                    PositionCapturedAt = now
                };
                safetyContext.SosIncidents.Add(activeSosIncident);
            }
            await safetyContext.SaveChangesAsync(cancellationToken);
        }

        var activeLowBatteryIncident = await safetyContext.LowBatteryIncidents.FirstOrDefaultAsync(
            i => i.SeniorId == senior.Id && i.Status != IncidentStatus.Resolved,
            cancellationToken);
        if (activeLowBatteryIncident is null)
        {
            var existingSeededLowBattery = await safetyContext.LowBatteryIncidents.FirstOrDefaultAsync(i => i.Id == lowBatteryIncidentId, cancellationToken);
            if (existingSeededLowBattery is not null)
            {
                existingSeededLowBattery.Status = IncidentStatus.Open;
                existingSeededLowBattery.BatteryLevelPercent = 15;
                existingSeededLowBattery.AcknowledgedAt = null;
                existingSeededLowBattery.AcknowledgedByAccountId = null;
                existingSeededLowBattery.ResolvedAt = null;
                existingSeededLowBattery.ResolvedByAccountId = null;
                existingSeededLowBattery.ResolutionNote = null;
                existingSeededLowBattery.OccurredAt = now.AddMinutes(-5);
                existingSeededLowBattery.ReceivedAt = now.AddMinutes(-5);
                existingSeededLowBattery.Version += 1;
                activeLowBatteryIncident = existingSeededLowBattery;
            }
            else
            {
                activeLowBatteryIncident = new LowBatteryIncident
                {
                    Id = lowBatteryIncidentId,
                    SeniorId = senior.Id,
                    DeviceId = device.Id,
                    Status = IncidentStatus.Open,
                    BatteryLevelPercent = 15,
                    OccurredAt = now.AddMinutes(-5),
                    ReceivedAt = now.AddMinutes(-5),
                    Latitude = 51.107883,
                    Longitude = 17.038538,
                    AccuracyMeters = 5.0,
                    SpeedMps = 0.0,
                    PositionCapturedAt = now.AddMinutes(-5)
                };
                safetyContext.LowBatteryIncidents.Add(activeLowBatteryIncident);
            }
            await safetyContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureIncidentNotificationAsync(
            notificationsContext,
            sosNotificationId,
            sosDeliveryId,
            caretaker.Id,
            senior.Id,
            NotificationType.Sos,
            "Wezwanie pomocy SOS",
            "== SOS ==\n\nJan Kowalski potrzebuje pomocy. Otwórz aplikację!",
            activeSosIncident.Id,
            now,
            cancellationToken);

        await EnsureIncidentNotificationAsync(
            notificationsContext,
            lowBatteryNotificationId,
            lowBatteryDeliveryId,
            caretaker.Id,
            senior.Id,
            NotificationType.LowBattery,
            "Niski poziom baterii",
            "Urządzenie seniora ma niski poziom baterii. Otwórz aplikację, aby sprawdzić zgłoszenie.",
            activeLowBatteryIncident.Id,
            now.AddMinutes(-5),
            cancellationToken);

        await SeedRoutineTasksAndOccurrencesAsync(routinesContext, senior.Id, caretaker.Id, now, cancellationToken);
        await SeedMedicalRecordsAsync(medicalContext, senior.Id, caretaker.Id, now, cancellationToken);
    }

    private static async Task<Account> EnsureAccountAsync(
        IdentityDbContext identityContext,
        PasswordHashing passwords,
        Guid id,
        Guid roleId,
        string email,
        string phoneNumber,
        string firstName,
        string lastName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToUpperInvariant();
        var account = await identityContext.Accounts.FirstOrDefaultAsync(
            a => a.Id == id || a.NormalizedPhoneNumber == phoneNumber || a.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (account is not null) return account;

        account = new Account
        {
            Id = id,
            RoleId = roleId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            PhoneNumber = phoneNumber,
            NormalizedPhoneNumber = phoneNumber,
            FirstName = firstName,
            LastName = lastName,
            Status = AccountStatus.Active,
            PhoneVerifiedAt = now,
            EmailVerifiedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            PasswordHash = ""
        };
        account.PasswordHash = passwords.Hash(account, "Password123!");
        identityContext.Accounts.Add(account);
        await identityContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    private static async Task EnsureRelationshipAsync(
        CareDbContext careContext,
        Guid id,
        Guid seniorId,
        Guid caretakerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var exists = await careContext.SeniorCareRelationships.AnyAsync(
            r => r.Id == id || (r.SeniorId == seniorId && r.CaretakerId == caretakerId && r.Status == RelationshipStatus.Active),
            cancellationToken);
        if (exists) return;

        careContext.SeniorCareRelationships.Add(new SeniorCareRelationship
        {
            Id = id,
            SeniorId = seniorId,
            CaretakerId = caretakerId,
            Status = RelationshipStatus.Active,
            CreatedAt = now
        });
        await careContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureHomeAddressAsync(
        SafetyDbContext safetyContext,
        Guid id,
        Guid seniorId,
        string postalCode,
        string city,
        string street,
        string buildingNumber,
        string apartmentNumber,
        double latitude,
        double longitude,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var exists = await safetyContext.HomeAddresses.AnyAsync(
            h => h.Id == id || (h.SeniorId == seniorId && h.DeletedAt == null),
            cancellationToken);
        if (exists) return;

        safetyContext.HomeAddresses.Add(new HomeAddress
        {
            Id = id,
            SeniorId = seniorId,
            CountryCode = "PL",
            PostalCode = postalCode,
            City = city,
            Street = street,
            BuildingNumber = buildingNumber,
            ApartmentNumber = apartmentNumber,
            Latitude = latitude,
            Longitude = longitude,
            CreatedByAccountId = seniorId,
            CreatedAt = now,
            UpdatedAt = now
        });
        await safetyContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureContactAsync(
        SeniorProfileDbContext seniorProfileContext,
        Guid id,
        Guid seniorId,
        Guid caretakerId,
        string displayName,
        string firstName,
        string lastName,
        string relationshipLabel,
        string phoneNumber,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var exists = await seniorProfileContext.SeniorContactPersons.AnyAsync(
            c => c.Id == id || (c.SeniorId == seniorId && c.DisplayName == displayName && c.DeletedAt == null),
            cancellationToken);
        if (exists) return;

        seniorProfileContext.SeniorContactPersons.Add(new SeniorContactPerson
        {
            Id = id,
            SeniorId = seniorId,
            DisplayName = displayName,
            FirstName = firstName,
            LastName = lastName,
            RelationshipLabel = relationshipLabel,
            PhoneNumber = phoneNumber,
            IsActive = true,
            CreatedByAccountId = caretakerId,
            CreatedAt = now,
            UpdatedAt = now
        });
        await seniorProfileContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureIncidentNotificationAsync(
        NotificationsDbContext notificationsContext,
        Guid notificationId,
        Guid deliveryId,
        Guid recipientAccountId,
        Guid seniorId,
        NotificationType type,
        string title,
        string message,
        Guid incidentId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var notification = await notificationsContext.Notifications.FirstOrDefaultAsync(
            n => n.Id == notificationId,
            cancellationToken);
        if (notification is null)
        {
            notification = new Notification
            {
                Id = notificationId,
                Source = "safety",
                IntegrationEventId = notificationId,
                RecipientAccountId = recipientAccountId,
                SeniorId = seniorId,
                Type = type,
                Title = title,
                Message = message,
                CreatedAt = createdAt,
                ReadAt = null,
                TargetKind = NotificationTargetKind.Incident,
                TargetId = incidentId
            };
            notificationsContext.Notifications.Add(notification);
            await notificationsContext.SaveChangesAsync(cancellationToken);
        }
        else if (notification.TargetId != incidentId || notification.ReadAt is not null)
        {
            notification.TargetId = incidentId;
            notification.ReadAt = null;
            notification.Version += 1;
            await notificationsContext.SaveChangesAsync(cancellationToken);
        }

        var deliveryExists = await notificationsContext.NotificationDeliveries.AnyAsync(
            d => d.Id == deliveryId || (d.NotificationId == notification.Id && d.Channel == DeliveryChannel.Sms),
            cancellationToken);
        if (!deliveryExists)
        {
            notificationsContext.NotificationDeliveries.Add(new NotificationDelivery
            {
                Id = deliveryId,
                NotificationId = notification.Id,
                Channel = DeliveryChannel.Sms,
                Status = DeliveryStatus.Delivered,
                AttemptCount = 1,
                SentAt = createdAt,
                DeliveredAt = createdAt
            });
            await notificationsContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedRoutineTasksAndOccurrencesAsync(
        RoutinesDbContext routinesContext,
        Guid seniorId,
        Guid caretakerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string timeZoneId = "Europe/Warsaw";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var todayLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var createdAt = now.AddDays(-7);

        (Guid TaskId, Guid ScheduleId, RoutineType Type, string Title, string Instructions, TimeOnly Time, OccurrenceStatus Status, TaskDecision? Decision)[] definitions =
        [
            (
                Guid.Parse("019a0000-0000-7000-8000-0000000000b0"),
                Guid.Parse("019a0000-0000-7000-8000-0000000000b3"),
                RoutineType.Medication,
                "Poranne leki",
                "1 tabletka po śniadaniu",
                new TimeOnly(6, 0),
                OccurrenceStatus.Confirmed,
                TaskDecision.Confirmed
            ),
            (
                Guid.Parse("019a0000-0000-7000-8000-0000000000b1"),
                Guid.Parse("019a0000-0000-7000-8000-0000000000b4"),
                RoutineType.Meal,
                "Śniadanie dietetyczne",
                "Ciepły posiłek poranny",
                new TimeOnly(7, 0),
                OccurrenceStatus.Pending,
                null
            ),
            (
                Guid.Parse("019a0000-0000-7000-8000-0000000000b2"),
                Guid.Parse("019a0000-0000-7000-8000-0000000000b5"),
                RoutineType.Hydration,
                "Szklanka wody",
                "Wypij 250 ml wody",
                new TimeOnly(6, 30),
                OccurrenceStatus.Skipped,
                TaskDecision.Skipped
            )
        ];

        foreach (var item in definitions)
        {
            var task = await routinesContext.RoutineTasks.FirstOrDefaultAsync(t => t.Id == item.TaskId, cancellationToken);
            if (task is null)
            {
                task = new RoutineTask
                {
                    Id = item.TaskId,
                    SeniorId = seniorId,
                    Type = item.Type,
                    Title = item.Title,
                    Instructions = item.Instructions,
                    StartsOn = todayLocalDate.AddDays(-7),
                    IsActive = true,
                    ScheduleRevision = 1,
                    CreatedByAccountId = caretakerId,
                    UpdatedByAccountId = caretakerId,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                };
                routinesContext.RoutineTasks.Add(task);

                routinesContext.RoutineTaskSchedules.Add(new RoutineTaskSchedule
                {
                    Id = item.ScheduleId,
                    RoutineTaskId = task.Id,
                    Revision = 1,
                    TimeZone = timeZoneId,
                    DaysOfWeek = [1, 2, 3, 4, 5, 6, 7],
                    TimeOfDay = item.Time
                });
                await routinesContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                if (task.Title != item.Title || task.Instructions != item.Instructions)
                {
                    task.Title = item.Title;
                    task.Instructions = item.Instructions;
                    task.UpdatedAt = now;
                    task.Version += 1;
                }
                var schedule = await routinesContext.RoutineTaskSchedules.FirstOrDefaultAsync(s => s.Id == item.ScheduleId, cancellationToken);
                if (schedule is not null && schedule.TimeOfDay != item.Time)
                {
                    schedule.TimeOfDay = item.Time;
                }
                var staleOccurrences = await routinesContext.TaskOccurrences
                    .Where(o => o.RoutineTaskId == task.Id && o.LocalTime != item.Time)
                    .ToListAsync(cancellationToken);
                if (staleOccurrences.Count > 0)
                {
                    routinesContext.TaskOccurrences.RemoveRange(staleOccurrences);
                }
                await routinesContext.SaveChangesAsync(cancellationToken);
            }

            var scheduledAt = LocalScheduleTime.Resolve(todayLocalDate, item.Time, zone);
            var occurrence = await routinesContext.TaskOccurrences.FirstOrDefaultAsync(
                o => o.RoutineTaskId == task.Id && o.ScheduleRevision == task.ScheduleRevision && o.LocalDate == todayLocalDate,
                cancellationToken);
            if (occurrence is null)
            {
                occurrence = new TaskOccurrence
                {
                    SeniorId = seniorId,
                    RoutineTaskId = task.Id,
                    ScheduleRevision = task.ScheduleRevision,
                    LocalDate = todayLocalDate,
                    LocalTime = item.Time,
                    ScheduledAt = scheduledAt,
                    TimeZone = timeZoneId,
                    Type = item.Type,
                    Title = item.Title,
                    Instructions = item.Instructions,
                    Status = item.Status,
                    UpdatedAt = now
                };
                routinesContext.TaskOccurrences.Add(occurrence);
                await routinesContext.SaveChangesAsync(cancellationToken);
            }
            else if (occurrence.Status != item.Status || occurrence.Title != item.Title || occurrence.LocalTime != item.Time)
            {
                occurrence.Title = item.Title;
                occurrence.Instructions = item.Instructions;
                occurrence.LocalTime = item.Time;
                occurrence.ScheduledAt = scheduledAt;
                occurrence.Status = item.Status;
                occurrence.UpdatedAt = now;
                occurrence.Version += 1;
                await routinesContext.SaveChangesAsync(cancellationToken);
            }

            if (item.Decision is { } decision)
            {
                var confirmationExists = await routinesContext.TaskConfirmations.AnyAsync(
                    c => c.TaskOccurrenceId == occurrence.Id,
                    cancellationToken);
                if (!confirmationExists)
                {
                    routinesContext.TaskConfirmations.Add(new TaskConfirmation
                    {
                        TaskOccurrenceId = occurrence.Id,
                        Decision = decision,
                        PerformedAt = now,
                        ReceivedAt = now,
                        RecordedByAccountId = seniorId
                    });
                    await routinesContext.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }

    private static async Task SeedMedicalRecordsAsync(
        MedicalRecordsDbContext medicalContext,
        Guid seniorId,
        Guid caretakerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        (Guid Id, string Title, string Description, MedicalRecordType Type, DateOnly DocumentDate)[] records =
        [
            (
                Guid.Parse("019a0000-0000-7000-8000-0000000000c0"),
                "Badanie morfologii krwi",
                "Kontrolne badanie laboratoryjne morfologii",
                MedicalRecordType.TestResult,
                new DateOnly(2026, 1, 15)
            ),
            (
                Guid.Parse("019a0000-0000-7000-8000-0000000000c1"),
                "Recepta kardiologiczna",
                "Recepta na stałe leki nasercowe",
                MedicalRecordType.Prescription,
                new DateOnly(2026, 2, 10)
            ),
            (
                Guid.Parse("019a0000-0000-7000-8000-0000000000c2"),
                "Wypis ze szpitala wojewódzkiego",
                "Karta informacyjna leczenia szpitalnego",
                MedicalRecordType.DischargeSummary,
                new DateOnly(2026, 3, 5)
            )
        ];

        foreach (var item in records)
        {
            var exists = await medicalContext.MedicalRecords.AnyAsync(r => r.Id == item.Id, cancellationToken);
            if (exists) continue;

            medicalContext.MedicalRecords.Add(new MedicalRecord
            {
                Id = item.Id,
                SeniorId = seniorId,
                Title = item.Title,
                Description = item.Description,
                RecordType = item.Type,
                DocumentDate = item.DocumentDate,
                CreatedByAccountId = caretakerId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await medicalContext.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
