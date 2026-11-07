using GarageStack.Core.Helpers;
using GarageStack.Core.Interfaces;

namespace GarageStack.Worker.Services;

/// <summary>
/// Runs <see cref="ChargeRecorder"/> for every vehicle on a timer. A charge counts as finished once
/// the car has not reported charging for <see cref="ChargeSegmenter.PauseThreshold"/>.
/// </summary>
public class ChargeRecorderService(
    ILogger<ChargeRecorderService> logger,
    IServiceScopeFactory scopeFactory) : PeriodicBackgroundService(logger)
{
    protected override string Name => "Charge recorder";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    protected override async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var vehicles = await scope.ServiceProvider.GetRequiredService<IVehicleRepository>().GetAllAsync(ct);
        var recorder = new ChargeRecorder(
            scope.ServiceProvider.GetRequiredService<ITelemetryRepository>(),
            scope.ServiceProvider.GetRequiredService<IChargeRepository>(),
            logger);

        foreach (var vehicle in vehicles)
        {
            var saved = await recorder.RecordAsync(vehicle.Id, DateTime.UtcNow, ct);
            if (saved > 0)
                logger.LogInformation("Saved {Count} finished charge(s) for VIN={Vin}", saved, LogRedaction.Vin(vehicle.Vin));
        }
    }
}
