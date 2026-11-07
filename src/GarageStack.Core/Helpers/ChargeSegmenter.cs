using GarageStack.Core.Models;

namespace GarageStack.Core.Helpers;

/// <summary>The charges cut from a run of readings: the finished ones, and where an unfinished one began.</summary>
public sealed record ChargeSegmentation(IReadOnlyList<ChargeSession> Closed, DateTime? OpenSince);

/// <summary>
/// Cuts charging readings into sessions. Readings that say the car is charging belong to one
/// session while they come no further apart than <see cref="PauseThreshold"/>, whatever is reported
/// in between: a load-balancing wallbox pauses a charge, and the car reports it stopped, without
/// anyone unplugging. A session is finished once that much time has passed after its last charging
/// reading with no new one.
/// </summary>
public static class ChargeSegmenter
{
    public static readonly TimeSpan PauseThreshold = TimeSpan.FromMinutes(20);

    // Power is integrated between readings no further apart than this; a longer silence is a gap in
    // what was reported, not a known power, and is left out rather than guessed.
    internal static readonly TimeSpan MaxIntegrationGap = TimeSpan.FromMinutes(15);

    // An MG's on-board charger tops out at 11 kW; anything clearly above it is a DC charger.
    internal const double DcPowerThresholdKw = 12.5;

    /// <param name="samples">Readings oldest first.</param>
    /// <param name="horizon">The time up to which readings are known to be complete.</param>
    public static ChargeSegmentation Segment(IReadOnlyList<ChargeSample> samples, DateTime horizon)
    {
        var closed = new List<ChargeSession>();
        List<ChargeSample>? current = null;
        DateTime? lastCharging = null;

        void Close()
        {
            if (current is { Count: > 0 }) closed.Add(Build(current));
            current = null;
            lastCharging = null;
        }

        for (var i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            if (s.IsCharging == true)
            {
                if (lastCharging is { } last && s.RecordedAt - last > PauseThreshold) Close();
                if (current is null)
                {
                    current = [];
                    // The reading just before a session may carry its starting charge level.
                    if (i > 0 && samples[i - 1].IsCharging != true) current.Add(samples[i - 1] with { IsCharging = false });
                }

                current.Add(s);
                lastCharging = s.RecordedAt;
            }
            else if (current is not null)
            {
                if (lastCharging is { } last && s.RecordedAt - last > PauseThreshold) Close();
                else current.Add(s);
            }
        }

        DateTime? openSince = null;
        if (current is not null && lastCharging is { } since)
        {
            if (horizon - since > PauseThreshold) Close();
            else openSince = current.First(c => c.IsCharging == true).RecordedAt;
        }

        return new ChargeSegmentation(closed, openSince);
    }

    private static ChargeSession Build(List<ChargeSample> run)
    {
        var charging = run.Where(s => s.IsCharging == true).ToList();
        var startedAt = charging[0].RecordedAt;
        var endedAt = charging[^1].RecordedAt;

        // Readings after the last charging one belong to the session only up to the pause threshold,
        // where the car reports its final charge level.
        var tail = run.Where(s => s.RecordedAt <= endedAt + PauseThreshold).ToList();

        var socStart = run.Where(s => s.RecordedAt <= startedAt && s.SocPercent is not null).Select(s => s.SocPercent).LastOrDefault()
                       ?? charging.Select(s => s.SocPercent).FirstOrDefault(v => v is not null);
        var socEnd = tail.Select(s => s.SocPercent).LastOrDefault(v => v is not null);

        var powers = charging.Where(s => s.PowerKw is not null).Select(s => Math.Abs(s.PowerKw!.Value)).ToList();
        double? maxPower = powers.Count > 0 ? powers.Max() : null;

        var type = charging.Select(s => s.ChargingType).LastOrDefault(t => !string.IsNullOrWhiteSpace(t));
        string? chargeType = type is not null && type.Contains("DC", StringComparison.OrdinalIgnoreCase) ? "DC"
            : maxPower > DcPowerThresholdKw ? "DC"
            : maxPower is not null || type is not null ? "AC"
            : null;

        var where = run.LastOrDefault(s => s.Latitude is not null && s.Longitude is not null && s.RecordedAt <= endedAt);

        return new ChargeSession
        {
            StartedAt = startedAt,
            EndedAt = endedAt,
            SocStartPercent = socStart,
            SocEndPercent = socEnd,
            EnergyAddedKwh = Integrate(charging),
            MaxPowerKw = maxPower,
            ChargeType = chargeType,
            Latitude = where?.Latitude,
            Longitude = where?.Longitude,
            OdometerKm = run.Select(s => s.OdometerKm).LastOrDefault(v => v is not null),
            StopReason = tail.Select(s => s.StopReason).LastOrDefault(r => !string.IsNullOrWhiteSpace(r)),
            SocTargetPercent = run.Select(s => s.SocTargetPercent).LastOrDefault(v => v is not null),
        };
    }

    /// <summary>
    /// The energy in kWh from the power readings, by the trapezoid rule between readings close
    /// enough together to trust. Null with fewer than two power readings.
    /// </summary>
    internal static double? Integrate(IReadOnlyList<ChargeSample> charging)
    {
        var points = charging.Where(s => s.PowerKw is not null).ToList();
        if (points.Count < 2) return null;

        double kwh = 0;
        for (var i = 1; i < points.Count; i++)
        {
            var span = points[i].RecordedAt - points[i - 1].RecordedAt;
            if (span <= TimeSpan.Zero || span > MaxIntegrationGap) continue;
            var avg = (Math.Abs(points[i].PowerKw!.Value) + Math.Abs(points[i - 1].PowerKw!.Value)) / 2;
            kwh += avg * span.TotalHours;
        }

        return kwh > 0 ? Math.Round(kwh, 2) : null;
    }
}
