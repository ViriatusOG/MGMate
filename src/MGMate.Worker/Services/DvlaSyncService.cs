using System.Globalization;
using MGMate.Core.Configuration;
using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;
using MGMate.Core.Models;
using MGMate.Data.Services;
using Microsoft.Extensions.Localization;

namespace MGMate.Worker.Services;

/// <summary>
/// Keeps each car's DVLA lookup current and says when road tax or the MOT needs attention: a month
/// and a week ahead, and once it has run out. Without a key (DVLA_API_KEY) it stops at once and
/// asks nothing.
/// </summary>
public class DvlaSyncService(
    ILogger<DvlaSyncService> logger,
    IServiceScopeFactory scopeFactory,
    IPushSender pushSender,
    DvlaOptions options,
    IStringLocalizer<NotificationStrings> strings,
    TimeProvider time) : PeriodicBackgroundService(logger)
{
    // The "due" categories name their date, so this only spaces out the two that repeat: an
    // untaxed car, and an MOT that has run out.
    private readonly NotificationCooldownGate _cooldownGate = new(TimeSpan.FromDays(30));

    protected override string Name => "DVLA sync";

    // Out of the way of the start-up rush; the API looks a plate up itself as soon as it is entered.
    protected override TimeSpan InitialDelay => TimeSpan.FromMinutes(6);

    // A pass that is not due a lookup only reads a row per car.
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Enabled) return base.ExecuteAsync(stoppingToken);

        logger.LogInformation("DVLA lookups are off (no DVLA_API_KEY); the DVLA sync does not run");
        return Task.CompletedTask;
    }

    protected override async Task RunOnceAsync(CancellationToken ct)
    {
        if (!options.Enabled) return;

        using var scope = scopeFactory.CreateScope();
        var vehicles = await scope.ServiceProvider.GetRequiredService<IVehicleRepository>().GetAllAsync(ct);
        var dvla = scope.ServiceProvider.GetRequiredService<IDvlaRepository>();
        var refresher = scope.ServiceProvider.GetRequiredService<DvlaRefresher>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var lookups = (await dvla.ListAsync(ct)).ToDictionary(i => i.VehicleId);

        foreach (var vehicle in vehicles)
        {
            if (vehicle.LicensePlate is null) continue;

            var info = lookups.GetValueOrDefault(vehicle.Id);
            if (refresher.IsDue(vehicle, info))
                info = await refresher.RefreshAsync(vehicle, ct);

            if (info is not { Found: true, FetchedAt: { } fetchedAt } || info.LicensePlate != vehicle.LicensePlate) continue;

            // Only what the DVLA said lately: tax renewed while the DVLA could not be reached must
            // not be reported as running out.
            var now = time.GetUtcNow().UtcDateTime;
            if (now - fetchedAt > 2 * options.RefreshInterval) continue;

            foreach (var alert in BuildAlerts(info, now, strings))
            {
                if (!await _cooldownGate.ShouldNotifyAsync(vehicle.Vin, vehicle.Id, alert.Category, notifications, ct)) continue;

                await pushSender.SendToAllAsync(alert.Title, alert.Body, ct, alert.Category, vehicle.Id);
                logger.LogInformation("DVLA push sent: {Vin}/{Category}", LogRedaction.Vin(vehicle.Vin), alert.Category);
            }
        }
    }

    internal readonly record struct DvlaAlert(string Category, string Title, string Body);

    /// <summary>The tax and MOT alerts the lookup calls for at <paramref name="utcNow"/>, before any cooldown.</summary>
    internal static IReadOnlyList<DvlaAlert> BuildAlerts(DvlaVehicleInfo info, DateTime utcNow, IStringLocalizer<NotificationStrings> strings)
    {
        var alerts = new List<DvlaAlert>();

        // A car declared off the road (SORN) is neither taxed nor untaxed: nothing to remind of.
        if (Is(info.TaxStatus, "Untaxed"))
        {
            alerts.Add(new(NotificationCategories.DvlaUntaxed, strings["DvlaUntaxedTitle"], strings["DvlaUntaxedBody"]));
        }
        else if (Is(info.TaxStatus, "Taxed") && info.TaxDueDate is { } taxDue)
        {
            var daysLeft = DvlaReminder.DaysLeft(taxDue, utcNow);
            if (NoticeDays(daysLeft) is { } notice)
                alerts.Add(new(NotificationCategories.DvlaTaxDue(notice, taxDue), strings["DvlaTaxDueTitle"],
                    DueBody("DvlaTaxDue", daysLeft, taxDue, strings)));
        }

        if (Is(info.MotStatus, "Not valid"))
        {
            alerts.Add(new(NotificationCategories.DvlaMotExpired, strings["DvlaMotExpiredTitle"], strings["DvlaMotExpiredBody"]));
        }
        else if (info.MotExpiryDate is { } motExpiry)
        {
            var daysLeft = DvlaReminder.DaysLeft(motExpiry, utcNow);
            if (daysLeft < 0)
                alerts.Add(new(NotificationCategories.DvlaMotExpired, strings["DvlaMotExpiredTitle"], strings["DvlaMotExpiredBody"]));
            else if (NoticeDays(daysLeft) is { } notice)
                alerts.Add(new(NotificationCategories.DvlaMotDue(notice, motExpiry), strings["DvlaMotDueTitle"],
                    DueBody("DvlaMotDue", daysLeft, motExpiry, strings)));
        }

        return alerts;
    }

    private static bool Is(string? status, string expected) =>
        string.Equals(status?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    // The same steps as the APK's: a month ahead and a week ahead. Null outside both, and once the
    // date has passed (by then the DVLA's status says so itself).
    private static int? NoticeDays(int daysLeft) => ApkReminder.StageOf(daysLeft) switch
    {
        ApkStage.DueWithinWeek => ApkReminder.WeekNoticeDays,
        ApkStage.DueWithinMonth => ApkReminder.MonthNoticeDays,
        _ => null,
    };

    private static string DueBody(string key, int daysLeft, DateOnly on, IStringLocalizer<NotificationStrings> strings)
    {
        var date = on.ToString("d MMMM yyyy", CultureInfo.CurrentUICulture);
        return daysLeft switch
        {
            0 => strings[key + "TodayBody", date],
            1 => strings[key + "TomorrowBody", date],
            _ => strings[key + "Body", date, daysLeft],
        };
    }
}
