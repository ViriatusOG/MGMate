using MGMate.Core.Helpers;
using MGMate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace MGMate.Data.Demo;

/// <summary>
/// Fills the in-memory demo database with the demo vehicle, a handful of maintenance items in
/// various due states, two climate schedules, a few notifications and an RDW lookup (with the two notifications the
/// Worker would have sent about it), so those pages and their dashboard cards have something to
/// show.
/// Telemetry and trips come from the in-memory demo repositories instead.
/// </summary>
public static class DemoSeeder
{
    /// <param name="db">The in-memory demo database.</param>
    /// <param name="rdwEnabled">Whether to seed the RDW lookup and its notifications: not when RDW_ENABLED=false.</param>
    /// <param name="ct">Cancels the seeding.</param>
    public static async Task SeedAsync(AppDbContext db, bool rdwEnabled = true, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);

        if (!await db.Vehicles.AnyAsync(ct))
        {
            db.Vehicles.Add(DemoVehicleRepository.DemoVehicle);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.ClimateSchedules.AnyAsync(ct))
            await SeedClimateSchedulesAsync(db, ct);

        if (!await db.AppNotifications.AnyAsync(ct))
            await SeedNotificationsAsync(db, ct);

        if (rdwEnabled && !await db.RdwVehicleInfos.AnyAsync(ct))
            await SeedRdwAsync(db, ct);

        if (!await db.ChargeSessions.AnyAsync(ct))
            await SeedChargesAsync(db, ct);

        if (!await db.WarrantyCovers.AnyAsync(ct))
        {
            db.WarrantyCovers.AddRange(WarrantyDefaults.For(DemoVehicleRepository.DemoVehicle.Id));
            await db.SaveChangesAsync(ct);
        }

        if (await db.MaintenanceItems.AnyAsync(ct))
            return;

        var vehicleId = DemoVehicleRepository.DemoVehicle.Id;
        var oilChange = new MaintenanceItem
        {
            VehicleId = vehicleId,
            Name = "Oil change",
            IntervalKm = 15_000,
            IntervalMonths = 12,
            LastServiceDate = DateTime.UtcNow.AddMonths(-7),
            LastServiceOdometerKm = 15_000,
        };
        var tyreRotation = new MaintenanceItem
        {
            VehicleId = vehicleId,
            Name = "Tyre rotation",
            IntervalKm = 10_000,
            LastServiceDate = DateTime.UtcNow.AddMonths(-4),
            LastServiceOdometerKm = 15_500,
        };
        var majorService = new MaintenanceItem
        {
            VehicleId = vehicleId,
            Name = "Major service",
            IntervalKm = 60_000,
            IntervalMonths = 60,
            LastServiceDate = DateTime.UtcNow.AddMonths(-61),
            LastServiceOdometerKm = 0,
        };
        var cabinFilter = new MaintenanceItem
        {
            VehicleId = vehicleId,
            Name = "Cabin air filter",
            IntervalKm = 15_000,
        };

        db.MaintenanceItems.AddRange(oilChange, tyreRotation, majorService, cabinFilter);
        await db.SaveChangesAsync(ct);

        // One log entry per item that has a service history, mirroring its baseline.
        db.MaintenanceLogEntries.AddRange(
            new[] { oilChange, tyreRotation, majorService }.Select(item => new MaintenanceLogEntry
            {
                MaintenanceItemId = item.Id,
                PerformedAt = item.LastServiceDate!.Value,
                OdometerKm = item.LastServiceOdometerKm,
            }));
        await db.SaveChangesAsync(ct);
    }

    // What the RDW lookup and the Worker's round after it would have left: the recall seen three
    // days ago and pushed then, and the APK's month notice, sent nine days before the three weeks
    // it has left now. There is no Worker in demo mode, so the texts are the English ones it sends.
    private static async Task SeedRdwAsync(AppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var vehicle = DemoVehicleRepository.DemoVehicle;
        var registration = DemoRdwClient.Registration(now);
        var seenAt = now.AddDays(-3);
        var recalls = RdwRecalls.Merge([], DemoRdwClient.Statuses, DemoRdwClient.Details, seenAt)
            .Select(r => r.IsOpen ? r with { NotifiedAt = seenAt } : r)
            .ToList();

        db.RdwVehicleInfos.Add(new RdwVehicleInfo
        {
            VehicleId = vehicle.Id,
            LicensePlate = vehicle.LicensePlate!,
            Found = true,
            Make = registration.Make,
            Model = registration.Model,
            ApkExpiresOn = registration.ApkExpiresOn,
            WamInsured = registration.WamInsured,
            OpenRecallIndicator = registration.OpenRecallIndicator,
            RecallsJson = RdwRecalls.Serialize(recalls),
            FetchedAt = now.AddHours(-2),
        });

        var openRecall = recalls.First(r => r.IsOpen);
        db.AppNotifications.AddRange(
            new AppNotification
            {
                Title = "Open recall",
                Body = $"The RDW lists an open recall ({openRecall.Code}): {openRecall.Defect}",
                CreatedAt = seenAt,
                Category = NotificationCategories.RdwRecall(openRecall.Code),
                VehicleId = vehicle.Id,
            },
            new AppNotification
            {
                Title = "APK due soon",
                Body = $"The APK runs out on {registration.ApkExpiresOn:dd-MM-yyyy}, in {ApkReminder.MonthNoticeDays} days.",
                CreatedAt = now.AddDays(-(ApkReminder.MonthNoticeDays - DemoRdwClient.ApkDaysLeft)),
                Category = NotificationCategories.RdwApkDue(ApkReminder.MonthNoticeDays, registration.ApkExpiresOn!.Value),
                VehicleId = vehicle.Id,
            });
        await db.SaveChangesAsync(ct);
    }

    // A few of the everyday notifications the Worker sends, worded as it words them, so the
    // notification panel has today's, yesterday's and an archived one to show.
    private static async Task SeedNotificationsAsync(AppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var vehicleId = DemoVehicleRepository.DemoVehicle.Id;
        AppNotification Make(string category, string title, string body, DateTime at, bool archived = false) =>
            new() { Category = category, Title = title, Body = body, CreatedAt = at, IsArchived = archived, VehicleId = vehicleId };

        db.AppNotifications.AddRange(
            Make(NotificationCategories.EngineStart, "Engine started", "Your car has been started.", now.AddMinutes(-13)),
            Make(NotificationCategories.ChargingComplete, "Charging Complete", "Your car has finished charging (SOC: 80%)",
                now.Hour >= 6 ? today.AddHours(5).AddMinutes(42) : now.AddMinutes(-40)),
            Make(NotificationCategories.LowTyre, "Low Tyre Pressure", "Tyre pressure low: RL", today.AddDays(-1).AddHours(7).AddMinutes(58)),
            Make(NotificationCategories.UnlockedParked, "Car Left Unlocked", "Your car is parked and unlocked",
                today.AddDays(-1).AddHours(18).AddMinutes(31)),
            Make(NotificationCategories.EngineStart, "Engine started", "Your car has been started.", today.AddDays(-4).AddHours(8), archived: true));
        await db.SaveChangesAsync(ct);
    }

    // Nothing runs them in demo mode (there is no Worker), so their next run is only what the
    // page shows.
    private static async Task SeedClimateSchedulesAsync(AppDbContext db, CancellationToken ct)
    {
        const string zoneId = "Europe/Amsterdam";
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone)) return;

        var weekdays = ClimateScheduleDays.Monday | ClimateScheduleDays.Tuesday | ClimateScheduleDays.Wednesday
            | ClimateScheduleDays.Thursday | ClimateScheduleDays.Friday;
        var work = new ClimateSchedule
        {
            VehicleId = DemoVehicleRepository.DemoVehicle.Id,
            Name = "Work",
            StartTime = new TimeOnly(7, 30),
            Days = weekdays,
            TimeZoneId = zoneId,
            Mode = ClimateScheduleMode.On,
            TemperatureC = 21,
            SeatLeftLevel = 2,
            LastRunAt = DateTime.UtcNow.AddDays(-1),
            LastRunOutcome = ClimateScheduleOutcome.Started,
        };
        var frostyMorning = new ClimateSchedule
        {
            VehicleId = DemoVehicleRepository.DemoVehicle.Id,
            StartTime = new TimeOnly(8, 15),
            Days = ClimateScheduleDays.None,
            TimeZoneId = zoneId,
            Mode = ClimateScheduleMode.FrontDefrost,
            RearDefroster = true,
            OnlyBelowC = 3,
        };
        foreach (var schedule in new[] { work, frostyMorning })
            schedule.NextRunUtc = ClimateScheduleCalendar.NextRunUtc(schedule.StartTime, schedule.Days, zone, DateTime.UtcNow);

        db.ClimateSchedules.AddRange(work, frostyMorning);
        await db.SaveChangesAsync(ct);
    }

    // Two months of charging: overnight at home every few days, and a rapid charge on a long day out.
    private static async Task SeedChargesAsync(AppDbContext db, CancellationToken ct)
    {
        var vehicleId = DemoVehicleRepository.DemoVehicle.Id;
        var today = DateTime.UtcNow.Date;
        var sessions = new List<ChargeSession>();
        var odometer = 14_200.0;
        for (var daysAgo = 60; daysAgo >= 1; daysAgo -= 3 + daysAgo % 2)
        {
            var rapid = daysAgo % 13 == 0;
            var startSoc = 22.0 + (daysAgo * 7) % 25;
            var endSoc = rapid ? 78.0 : 80.0 + (daysAgo % 3 == 0 ? 20 : 0);
            var started = rapid ? today.AddDays(-daysAgo).AddHours(13).AddMinutes(daysAgo % 40) : today.AddDays(-daysAgo).AddHours(22).AddMinutes(30 + daysAgo % 25);
            var minutes = rapid ? 26 + daysAgo % 9 : (endSoc - startSoc) * 0.64 * 60 / 7.0;
            odometer += 140 + daysAgo % 60;
            sessions.Add(new ChargeSession
            {
                VehicleId = vehicleId,
                StartedAt = started,
                EndedAt = started.AddMinutes(minutes),
                SocStartPercent = startSoc,
                SocEndPercent = endSoc,
                // 64 kWh usable, give or take what a real reading wanders by.
                EnergyAddedKwh = Math.Round((endSoc - startSoc) / 100 * (64 + Math.Sin(daysAgo) * 0.8), 2),
                // An on-board charger loses around a tenth as heat; a DC charge bypasses it.
                ChargerEnergyKwh = rapid ? null : Math.Round((endSoc - startSoc) / 100 * (64 + Math.Sin(daysAgo) * 0.8) / (0.89 + (daysAgo % 5) * 0.005), 2),
                MaxPowerKw = rapid ? 96.4 : 7.2,
                ChargeType = rapid ? "DC" : "AC",
                Latitude = rapid ? 52.0907 : 52.3676,
                Longitude = rapid ? 5.1214 : 4.9041,
                OdometerKm = Math.Round(odometer),
                StopReason = rapid ? "Stopped by user" : "Target reached",
                SocTargetPercent = (int)endSoc,
                // Marked rather than left to a home place, which the demo has none of until one is added.
                Place = rapid ? "public" : "home",
                PaidAmount = rapid ? Math.Round((decimal)((endSoc - startSoc) / 100 * 64 * 0.69), 2) : null,
            });
        }
        db.ChargeSessions.AddRange(sessions);
        await db.SaveChangesAsync(ct);
    }
}
