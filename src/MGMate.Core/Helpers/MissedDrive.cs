using MGMate.Core.Models;

namespace MGMate.Core.Helpers;

/// <summary>
/// Drives the gateway never followed. It only polls the car live once MG reports that the car
/// started, and MG does not always say so (a start with the phone's digital key, for one), so the
/// car can be driven with no fix taken on the way. What is left is two parked positions in a row
/// with the odometer higher at the second: such a drive is saved as a trip without a route, from
/// the one spot to the other, with the distance the odometer gives.
/// </summary>
public static class MissedDrive
{
    /// <summary>Less than this on the odometer is rounding or a reading that came in late, not a drive.</summary>
    public const double MinDistanceKm = 1.0;

    /// <summary>Two fixes closer together than this are one poll, not two ends of a drive.</summary>
    public static readonly TimeSpan MinGap = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The pairs of fixes in a row, both standing still, at least <see cref="MinGap"/> apart and
    /// with no saved trip between them: the ends of a drive nobody followed, if the odometer moved.
    /// </summary>
    /// <param name="fixes">GPS fixes, oldest first.</param>
    /// <param name="trips">When each saved trip near the fixes started and ended, hidden ones too.</param>
    public static IReadOnlyList<(TripPoint Before, TripPoint After)> Candidates(
        IReadOnlyList<TripPoint> fixes, IReadOnlyList<(DateTime StartedAt, DateTime EndedAt)> trips)
    {
        var pairs = new List<(TripPoint, TripPoint)>();
        for (var i = 1; i < fixes.Count; i++)
        {
            var before = fixes[i - 1];
            var after = fixes[i];
            if (IsMoving(before) || IsMoving(after)) continue;
            if (after.RecordedAt - before.RecordedAt < MinGap) continue;
            if (trips.Any(t => t.StartedAt <= after.RecordedAt && t.EndedAt >= before.RecordedAt)) continue;
            pairs.Add((before, after));
        }

        return pairs;
    }

    /// <summary>
    /// The trip for a pair from <see cref="Candidates"/>, or null when the odometer did not move far
    /// enough between them. It starts and ends when the car was found at its new spot: when it left
    /// is not known, only that it was after <paramref name="before"/>.
    /// </summary>
    public static TripDto? Build(TripPoint before, TripPoint after, double? odometerBeforeKm, double? odometerAfterKm)
    {
        if (odometerBeforeKm is not { } from || odometerAfterKm is not { } to) return null;
        var distance = to - from;
        if (distance < MinDistanceKm) return null;

        // No speeds: nothing was measured on the way, so the trip has no top or average speed.
        TripPoint[] points =
        [
            before with { Speed = null, Source = TripPointSource.Anchored },
            after with { Speed = null, Source = TripPointSource.Anchored },
        ];
        return new TripDto(0, after.RecordedAt, after.RecordedAt, Math.Round(distance, 2), points.Length, points);
    }

    // A fix without a speed comes from the position topic; a drive the gateway followed is saved as a
    // trip, and the trip check above leaves its fixes out either way.
    private static bool IsMoving(TripPoint fix) => fix.Speed is > 0;
}
