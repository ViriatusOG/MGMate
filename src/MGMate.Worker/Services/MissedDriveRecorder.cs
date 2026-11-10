using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;
using MGMate.Core.Models;

namespace MGMate.Worker.Services;

/// <summary>
/// Saves the drives the gateway never followed (<see cref="MissedDrive"/>) as trips without a
/// route. Runs after <see cref="TripRecorder"/>, over the last few days behind its recording line,
/// where every followed drive is saved already: a stretch with no trip in it there is final. The
/// trip it saves sits between the same two fixes, so looking again never saves it twice.
/// </summary>
public class MissedDriveRecorder(ITelemetryRepository telemetry, ITripRepository trips, ILogger logger)
{
    private readonly TripEnergyReader energy = new(telemetry);

    // How far behind the recording line it looks. The Worker runs every few minutes, so a missed
    // drive is found as soon as the car is seen at its new spot; the rest covers the Worker being
    // down for a while, and the drives missed before this existed.
    internal static readonly TimeSpan Lookback = TimeSpan.FromDays(3);

    // Trips are looked up from this much before the fixes, so one that began before them still
    // counts as covering them.
    private static readonly TimeSpan TripLookback = TimeSpan.FromDays(1);

    /// <returns>How many trips were saved.</returns>
    public async Task<int> RecordAsync(int vehicleId, CancellationToken ct)
    {
        if (await trips.GetRecordedUntilAsync(vehicleId, ct) is not { } until) return 0;

        var from = until - Lookback;
        var fixes = await telemetry.GetGpsFixesAsync(vehicleId, from, until, ct);
        if (fixes.Count < 2) return 0;

        var saved = (await trips.GetLogAsync(vehicleId, from - TripLookback, until, ct))
            .Select(t => (t.StartedAt, t.EndedAt))
            .ToList();

        var found = new List<RecordedTrip>();
        foreach (var (before, after) in MissedDrive.Candidates(fixes, saved))
        {
            // Read a little after the car was last seen parked, so the last reading of a drive that
            // ended there, which can come a poll late, is not taken for this one.
            var odometerBefore = await telemetry.GetOdometerAtAsync(vehicleId, before.RecordedAt + TripSegmenter.ParkThreshold, ct);
            var odometerAfter = await telemetry.GetOdometerAtAsync(vehicleId, after.RecordedAt + TripRecorder.SettleTime, ct);
            if (MissedDrive.Build(before, after, odometerBefore, odometerAfter) is not { } trip) continue;

            // The battery from when it was last seen parked to once it was found again. The outside
            // temperature over those hours is the weather while parked, not on the drive.
            var used = await energy.ReadAsync(vehicleId, before.RecordedAt, after.RecordedAt, trip.DistanceKm, ct);
            found.Add(new RecordedTrip(trip, odometerBefore, odometerAfter, used with { OutsideTemperature = null }, before.RecordedAt));
            logger.LogInformation(
                "Saving a drive without a route for vehicleId={VehicleId}: {Distance:F1} km on the odometer between {Before:o} and {After:o}, with no fix on the way",
                vehicleId, trip.DistanceKm, before.RecordedAt, after.RecordedAt);
        }

        if (found.Count > 0) await trips.SaveRecordedAsync(vehicleId, found, until, ct);
        return found.Count;
    }
}
