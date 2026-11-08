using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;
using MGMate.Core.Models;

namespace MGMate.Worker.Services;

/// <summary>
/// Reads a trip's <see cref="TripEnergy"/> off the telemetry: the last readings before it set off,
/// and the last ones once it had parked. The car reports its final figures a poll or two after it
/// stops, so the end is read as late as a trip end allows, as the odometer is.
/// </summary>
public sealed class TripEnergyReader(ITelemetryRepository telemetry)
{
    public async Task<TripEnergy> ReadAsync(
        int vehicleId, DateTime startedAt, DateTime endedAt, double distanceKm, CancellationToken ct)
    {
        var settled = endedAt + TripSegmenter.ParkThreshold;
        var energy = TripEnergyCalculator.Compute(
            startedAt,
            distanceKm,
            await telemetry.GetSocAtAsync(vehicleId, startedAt, ct),
            await telemetry.GetSocAtAsync(vehicleId, settled, ct),
            await telemetry.GetDayCountersAtAsync(vehicleId, startedAt, ct),
            await telemetry.GetDayCountersAtAsync(vehicleId, settled, ct));
        var outside = await telemetry.GetMeanOutsideTemperatureAsync(vehicleId, startedAt, endedAt, ct);
        return energy with { OutsideTemperature = outside is { } t ? Math.Round(t, 1) : null };
    }
}
