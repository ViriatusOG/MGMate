using GarageStack.Core.Helpers;
using GarageStack.Core.Interfaces;

namespace GarageStack.Worker.Services;

/// <summary>
/// Saves a vehicle's finished charges, walking its readings from the recording line a week at a
/// time, as <see cref="TripRecorder"/> does with trips. On an install that has never recorded one,
/// the line starts at the first charging reading, so the whole history is saved on the first run.
/// </summary>
public class ChargeRecorder(ITelemetryRepository telemetry, IChargeRepository charges, ILogger logger)
{
    internal static readonly TimeSpan Chunk = TimeSpan.FromDays(7);

    /// <returns>How many charges were saved.</returns>
    public async Task<int> RecordAsync(int vehicleId, DateTime now, CancellationToken ct)
    {
        var from = await charges.GetRecordedUntilAsync(vehicleId, ct)
                   ?? await telemetry.GetFirstChargingAtAsync(vehicleId, ct);
        if (from is null) return 0;

        var settled = now - TripRecorder.SettleTime;
        var saved = 0;
        var cursor = from.Value;

        while (cursor < settled)
        {
            var until = cursor + Chunk < settled ? cursor + Chunk : settled;
            var isLatest = until == settled;

            // The readings after the window too, up to the pause threshold, so a charge that ends
            // near the window's edge gets its final charge level.
            var samples = await telemetry.GetChargeSamplesAsync(vehicleId, cursor, until, ct);
            var segmentation = ChargeSegmenter.Segment(samples, until);

            if (segmentation.OpenSince == cursor && !isLatest)
            {
                // Charging for a whole week without a pause is a stuck reading; cut it at the week.
                logger.LogWarning(
                    "Charge for vehicleId={VehicleId} starting {Start:o} did not end within {Days} days; saving it cut at {End:o}",
                    vehicleId, cursor, Chunk.TotalDays, until);
                segmentation = ChargeSegmenter.Segment(samples, DateTime.MaxValue);
            }
            else if (segmentation.OpenSince == cursor)
            {
                break;
            }

            var next = segmentation.OpenSince ?? until;
            await charges.SaveRecordedAsync(vehicleId, segmentation.Closed, next, ct);
            saved += segmentation.Closed.Count;

            if (isLatest) break;
            cursor = next;
        }

        return saved;
    }

    /// <summary>
    /// Works out the charger's energy for charges saved before it was recorded, from their readings.
    /// A charge whose readings carry no plausible charger power stays without one.
    /// </summary>
    /// <returns>How many charges gained the figure.</returns>
    public async Task<int> BackfillChargerEnergyAsync(int vehicleId, CancellationToken ct)
    {
        var filled = 0;
        foreach (var session in await charges.GetWithoutChargerEnergyAsync(vehicleId, ct))
        {
            var samples = await telemetry.GetChargeSamplesAsync(
                vehicleId, session.StartedAt, session.EndedAt.AddSeconds(1), ct);
            var charging = samples.Where(s => s.IsCharging == true).ToList();
            if (ChargeSegmenter.ChargerEnergy(charging, session.EnergyAddedKwh) is not { } kwh) continue;
            await charges.SetChargerEnergyAsync(vehicleId, session.Id, kwh, ct);
            filled++;
        }
        return filled;
    }
}
