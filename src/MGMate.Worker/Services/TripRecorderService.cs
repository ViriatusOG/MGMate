using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;

namespace MGMate.Worker.Services;

/// <summary>
/// Runs <see cref="TripRecorder"/> for every vehicle on a timer. A trip counts as finished five
/// minutes after the car parks, or half an hour after the last fix, so checking every few minutes
/// saves it soon after that without asking the database anything in between. Drives the gateway
/// never followed are saved after them (<see cref="MissedDriveRecorder"/>).
/// </summary>
public class TripRecorderService(
    ILogger<TripRecorderService> logger,
    IServiceScopeFactory scopeFactory) : PeriodicBackgroundService(logger)
{
    protected override string Name => "Trip recorder";

    // No initial delay, so a fresh install saves its history right away.
    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    protected override async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var vehicles = await scope.ServiceProvider.GetRequiredService<IVehicleRepository>().GetAllAsync(ct);
        var recorder = new TripRecorder(
            scope.ServiceProvider.GetRequiredService<ITelemetryRepository>(),
            scope.ServiceProvider.GetRequiredService<ITripRepository>(),
            logger);
        var missed = new MissedDriveRecorder(
            scope.ServiceProvider.GetRequiredService<ITelemetryRepository>(),
            scope.ServiceProvider.GetRequiredService<ITripRepository>(),
            logger);

        foreach (var vehicle in vehicles)
        {
            var saved = await recorder.RecordAsync(vehicle.Id, DateTime.UtcNow, ct);
            if (saved > 0)
                logger.LogInformation("Saved {Count} finished trip(s) for VIN={Vin}", saved, LogRedaction.Vin(vehicle.Vin));

            // After the followed drives are saved, so only the stretches no trip covers are left.
            var unfollowed = await missed.RecordAsync(vehicle.Id, ct);
            if (unfollowed > 0)
                logger.LogInformation("Saved {Count} drive(s) without a route for VIN={Vin}", unfollowed, LogRedaction.Vin(vehicle.Vin));
        }
    }
}
