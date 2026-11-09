using MGMate.Core.Models;

namespace MGMate.Core.Helpers;

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

    // No AC supply charges faster than 22 kW (three-phase 32 A). A higher reading on an AC charge is
    // the car drawing on the battery while plugged in, such as pre-heating, not charging power.
    internal const double AcMaxPowerKw = 22;

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

        var chargeType = ChargeTypeOf(charging);
        charging = ChargingPower(charging, chargeType);
        var powers = charging.Where(s => s.PowerKw is not null).Select(s => Math.Abs(s.PowerKw!.Value)).ToList();
        double? maxPower = powers.Count > 0 ? powers.Max() : null;

        var where = run.LastOrDefault(s => s.Latitude is not null && s.Longitude is not null && s.RecordedAt <= endedAt);

        return new ChargeSession
        {
            StartedAt = startedAt,
            EndedAt = endedAt,
            SocStartPercent = socStart,
            SocEndPercent = socEnd,
            EnergyAddedKwh = Integrate(charging),
            ChargerEnergyKwh = chargeType == "DC" ? null : ChargerEnergy(charging, Integrate(charging)),
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
    /// "AC" or "DC": the charging type the car reports, else from the typical power while charging.
    /// The typical (median) power rather than the highest, so a moment of the car drawing hard on
    /// the battery, as pre-heating does, does not make a home charge a rapid one.
    /// </summary>
    public static string? ChargeTypeOf(IReadOnlyList<ChargeSample> charging)
    {
        var type = charging.Select(s => s.ChargingType).LastOrDefault(t => !string.IsNullOrWhiteSpace(t));
        if (type is not null && type.Contains("DC", StringComparison.OrdinalIgnoreCase)) return "DC";
        var powers = charging.Where(s => s.PowerKw is not null).Select(s => Math.Abs(s.PowerKw!.Value)).Order().ToList();
        if (powers.Count > 0 && type is null && powers[powers.Count / 2] > DcPowerThresholdKw) return "DC";
        return powers.Count > 0 || type is not null ? "AC" : null;
    }

    /// <summary>
    /// The charging readings with any power that cannot be charging power left out (null): readings
    /// of the other sign than most of the charge, which is the battery being drawn on rather than
    /// filled, and on an AC charge anything an AC supply cannot deliver. The readings themselves stay
    /// saved as reported; only what the charge's figures and chart are worked out from changes.
    /// </summary>
    public static List<ChargeSample> ChargingPower(IReadOnlyList<ChargeSample> charging, string? chargeType)
    {
        var signs = charging.Where(s => s.PowerKw is { } p && p != 0).Sum(s => Math.Sign(s.PowerKw!.Value));
        return [.. charging.Select(s => s.PowerKw is { } p && !IsChargingPower(p, signs, chargeType) ? s with { PowerKw = null } : s)];
    }

    private static bool IsChargingPower(double powerKw, int signs, string? chargeType) =>
        !(signs != 0 && powerKw != 0 && Math.Sign(powerKw) != Math.Sign(signs))
        && !(chargeType == "AC" && Math.Abs(powerKw) > AcMaxPowerKw);

    // An on-board charger turns at least this share of its input into battery energy, and never all
    // of it: a figure outside the range is the wrong phase guess or a broken reading.
    internal const double MinChargerEfficiency = 0.7;
    internal const double MaxChargerEfficiency = 0.995;

    /// <summary>
    /// The energy the on-board charger drew from the supply, in kWh. The gateway works the input
    /// power out both as a single-phase and as a three-phase supply without knowing which it is, so
    /// the one whose total the battery energy can have come from is taken; null when neither fits,
    /// or without battery energy to check against.
    /// </summary>
    public static double? ChargerEnergy(IReadOnlyList<ChargeSample> charging, double? batteryKwh)
    {
        if (batteryKwh is not { } battery || battery <= 0) return null;
        foreach (var pick in new Func<ChargeSample, double?>[] { s => s.ObcPowerSinglePhaseW, s => s.ObcPowerThreePhaseW })
        {
            var kwh = Integrate(charging
                .Where(s => pick(s) is > 0)
                .Select(s => s with { PowerKw = pick(s)! / 1000 })
                .ToList());
            if (kwh is { } total && battery / total is >= MinChargerEfficiency and <= MaxChargerEfficiency)
                return total;
        }
        return null;
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
