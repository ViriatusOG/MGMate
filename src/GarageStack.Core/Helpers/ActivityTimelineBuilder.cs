using GarageStack.Core.Models;

namespace GarageStack.Core.Helpers;

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

    public static ActivityTimeline Build(
        IReadOnlyList<ActivitySample> samples, DateTime from, DateTime to, TimeSpan gap)
    {
        var events = new List<ActivityEvent>();
        var gaps = new List<ActivityGap>();
        // Each switch keeps the last value it saw, so a reading that leaves it out changes nothing.
        var last = new bool?[Switches.Length];
        double? soc = null;
        DateTime? previous = null;

        foreach (var s in samples)
        {
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

        return new ActivityTimeline(events, gaps, hours, samples.Count);
    }
}
