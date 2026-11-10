using MGMate.Core.Models;

namespace MGMate.Core.Helpers;

/// <summary>
/// Turns a period of telemetry into what happened: the moments a state flipped, the stretches the
/// car went quiet, and how many readings arrived each hour. Read from the readings every time, so
/// nothing has to be recorded for it and it covers everything already stored.
/// </summary>
public static class ActivityTimelineBuilder
{
    public static readonly TimeSpan DefaultGap = TimeSpan.FromHours(1);

    private static readonly (Func<ActivitySample, bool?> Read, string On, string Off)[] Switches =
    [
        (s => s.IsLocked, "locked", "unlocked"),
        (s => s.AnyDoorOpen, "doorOpened", "doorsClosed"),
        (s => s.ChargerConnected, "plugged", "unplugged"),
        (s => s.IsCharging, "chargingStarted", "chargingStopped"),
        (s => s.ClimateOn, "climateOn", "climateOff"),
        (s => s.EngineRunning, "driveStarted", "driveEnded"),
    ];

    /// <param name="socBefore">
    /// The level as last reported before <paramref name="from"/>, if any. A parked car can go hours
    /// without a reading, and the level it last reported still holds, so the line starts with it at
    /// the start of the period instead of only at the first reading inside it.
    /// </param>
    public static ActivityTimeline Build(
        IReadOnlyList<ActivitySample> samples, DateTime from, DateTime to, TimeSpan gap, double? socBefore = null)
    {
        var events = new List<ActivityEvent>();
        var gaps = new List<ActivityGap>();
        var socPoints = new List<ActivitySocPoint>();
        // Each switch keeps the last value it saw, so a reading that leaves it out changes nothing.
        var last = new bool?[Switches.Length];
        double? soc = socBefore;
        DateTime? socAt = socBefore is null ? null : from;
        DateTime? previous = null;
        if (socBefore is { } carried) socPoints.Add(new ActivitySocPoint(from, carried));

        foreach (var s in samples)
        {
            // The level line for the overview: a point where the level moves, and the reading just
            // before it too, so a line drawn through them stays flat until the change.
            if (s.SocPercent is { } level)
            {
                if (soc is not { } prevLevel)
                    socPoints.Add(new ActivitySocPoint(s.RecordedAt, level));
                else if (prevLevel != level)
                {
                    if (socAt is { } flatEnd && socPoints[^1].At != flatEnd)
                        socPoints.Add(new ActivitySocPoint(flatEnd, prevLevel));
                    socPoints.Add(new ActivitySocPoint(s.RecordedAt, level));
                }
                socAt = s.RecordedAt;
            }
            soc = s.SocPercent ?? soc;
            if (previous is { } p && s.RecordedAt - p > gap) gaps.Add(new ActivityGap(p, s.RecordedAt));
            previous = s.RecordedAt;

            for (var i = 0; i < Switches.Length; i++)
            {
                if (Switches[i].Read(s) is not { } value) continue;
                if (last[i] is { } before && before != value)
                    events.Add(new ActivityEvent(s.RecordedAt, value ? Switches[i].On : Switches[i].Off, soc));
                last[i] = value;
            }
        }

        // Quiet at either end of the period counts as well: a car that stopped reporting yesterday
        // shows the whole of today as a gap, not as nothing at all.
        var end = to < DateTime.UtcNow ? to : DateTime.UtcNow;
        if (samples.Count == 0)
        {
            if (end - from > gap) gaps.Add(new ActivityGap(from, end));
        }
        else
        {
            if (samples[0].RecordedAt - from > gap) gaps.Insert(0, new ActivityGap(from, samples[0].RecordedAt));
            if (end - samples[^1].RecordedAt > gap) gaps.Add(new ActivityGap(samples[^1].RecordedAt, end));
        }

        var hours = samples
            .GroupBy(s => new DateTime(s.RecordedAt.Year, s.RecordedAt.Month, s.RecordedAt.Day, s.RecordedAt.Hour, 0, 0, DateTimeKind.Utc))
            .Select(g => new ActivityHour(g.Key, g.Count()))
            .OrderBy(h => h.Hour)
            .ToList();

        // The level as of the last reading, so the line reaches the end of the readings.
        if (soc is { } lastLevel && socAt is { } lastAt && socPoints[^1].At != lastAt)
            socPoints.Add(new ActivitySocPoint(lastAt, lastLevel));

        return new ActivityTimeline(events, gaps, hours, samples.Count, socPoints);
    }
}
