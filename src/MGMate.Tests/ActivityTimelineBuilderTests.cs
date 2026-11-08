using MGMate.Core.Helpers;
using MGMate.Core.Models;

namespace MGMate.Tests;

public class ActivityTimelineBuilderTests
{
    private static readonly DateTime T0 = new(2026, 11, 7, 8, 0, 0, DateTimeKind.Utc);

    private static ActivitySample S(int minutes, bool? locked = null, bool? charging = null, double? soc = null) =>
        new(T0.AddMinutes(minutes), locked, null, null, charging, null, null, soc);

    [Fact]
    public void Build_FindsTheMomentsAStateFlips()
    {
        var samples = new[]
        {
            S(0, locked: true, charging: false, soc: 50),
            S(5, locked: false),
            S(10),                       // says nothing about the lock: not a change
            S(15, locked: true, charging: true, soc: 51),
            S(20, charging: false, soc: 60),
        };

        var timeline = ActivityTimelineBuilder.Build(samples, T0, T0.AddMinutes(25), TimeSpan.FromHours(1));

        Assert.Equal(["unlocked", "locked", "chargingStarted", "chargingStopped"], timeline.Events.Select(e => e.Kind));
        Assert.Equal(60, timeline.Events[^1].SocPercent);
        Assert.Empty(timeline.Gaps);
        Assert.Equal(5, timeline.Readings);
        Assert.Single(timeline.Hours);
    }

    [Fact]
    public void Build_MarksQuietStretchesIncludingAtTheEnds()
    {
        var samples = new[] { S(90), S(100), S(300) };

        var timeline = ActivityTimelineBuilder.Build(samples, T0, T0.AddMinutes(420), TimeSpan.FromHours(1));

        Assert.Equal(
            [new ActivityGap(T0, T0.AddMinutes(90)), new ActivityGap(T0.AddMinutes(100), T0.AddMinutes(300)), new ActivityGap(T0.AddMinutes(300), T0.AddMinutes(420))],
            timeline.Gaps);
    }

    [Fact]
    public void Build_WithNoReadings_IsOneGap()
    {
        var timeline = ActivityTimelineBuilder.Build([], T0, T0.AddHours(3), TimeSpan.FromHours(1));
        Assert.Equal([new ActivityGap(T0, T0.AddHours(3))], timeline.Gaps);
    }

    [Fact]
    public void Build_KeepsTheLevelOnlyWhereItMoves()
    {
        var samples = new[]
        {
            S(0, soc: 80),
            S(10, soc: 80),
            S(20),                       // no level: nothing
            S(30, soc: 80),              // last reading before the drop: kept so the line stays flat
            S(40, soc: 70),
            S(50, soc: 70),              // last reading: kept so the line reaches it
        };

        var timeline = ActivityTimelineBuilder.Build(samples, T0, T0.AddMinutes(60), TimeSpan.FromHours(1));

        Assert.Equal(
            [
                new ActivitySocPoint(T0, 80), new ActivitySocPoint(T0.AddMinutes(30), 80),
                new ActivitySocPoint(T0.AddMinutes(40), 70), new ActivitySocPoint(T0.AddMinutes(50), 70),
            ],
            timeline.Soc);
    }

    [Fact]
    public void Build_WithNoLevels_HasNoLevelLine()
    {
        var timeline = ActivityTimelineBuilder.Build([S(0, locked: true)], T0, T0.AddMinutes(10), TimeSpan.FromHours(1));
        Assert.Empty(timeline.Soc);
    }
}
