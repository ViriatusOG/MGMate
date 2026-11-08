using MGMate.Core.Helpers;
using MGMate.Core.Models;

namespace MGMate.Tests;

public class TripEnergyCalculatorTests
{
    private static readonly DateTime Start = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime After = Start.AddMinutes(30);

    private static TripEnergy Compute(
        double distanceKm,
        TimedReading? socStart = null,
        TimedReading? socEnd = null,
        DayCounterReading? countersStart = null,
        DayCounterReading? countersEnd = null) =>
        TripEnergyCalculator.Compute(Start, distanceKm, socStart, socEnd, countersStart, countersEnd);

    [Fact]
    public void Soc_IsReadAtEitherEnd()
    {
        var energy = Compute(20, new(Start.AddHours(-3), 80), new(After, 74.5));

        Assert.Equal(80, energy.SocStartPercent);
        Assert.Equal(74.5, energy.SocEndPercent);
    }

    [Fact]
    public void SocEnd_FromBeforeTheTrip_IsNotAnEndReading()
    {
        // The car sent nothing after it set off, so the "end" lookup found the start reading again.
        var reading = new TimedReading(Start.AddMinutes(-10), 80);

        var energy = Compute(20, reading, reading);

        Assert.Equal(80, energy.SocStartPercent);
        Assert.Null(energy.SocEndPercent);
    }

    [Fact]
    public void Counters_ThatMovedByTheTripsDistance_GiveItsUse()
    {
        var energy = Compute(20,
            countersStart: new(Start.AddMinutes(-5), 3.2, 15),
            countersEnd: new(After, 6.8, 35.4));

        Assert.Equal(3.6, energy.CounterUsed!.Value, 6);
    }

    [Fact]
    public void Counters_ResetBeforeTheTrip_AreReadFromTheEndAlone()
    {
        // Yesterday's totals before the first drive of the day.
        var energy = Compute(12,
            countersStart: new(Start.AddHours(-10), 9.5, 60),
            countersEnd: new(After, 2.1, 12.3));

        Assert.Equal(2.1, energy.CounterUsed);
    }

    [Fact]
    public void Counters_ThatCoverMoreThanTheTrip_AreNotTrusted()
    {
        // The day's distance moved by far more than this trip: something in between went unseen.
        var energy = Compute(5,
            countersStart: new(Start.AddMinutes(-5), 1.0, 10),
            countersEnd: new(After, 9.0, 60));

        Assert.Null(energy.CounterUsed);
    }

    [Fact]
    public void Counters_NotUpdatedSinceTheStart_GiveNothing()
    {
        var reading = new DayCounterReading(Start.AddMinutes(-5), 1.0, 10);

        Assert.Null(Compute(10, countersStart: reading, countersEnd: reading).CounterUsed);
    }

    [Fact]
    public void Counters_WithoutTheDistance_GiveNothing()
    {
        var energy = Compute(10,
            countersStart: new(Start.AddMinutes(-5), 1.0, null),
            countersEnd: new(After, 3.0, null));

        Assert.Null(energy.CounterUsed);
    }

    [Fact]
    public void Counters_ThatUsedNothing_HaveNotCaughtUp()
    {
        var energy = Compute(10,
            countersStart: new(Start.AddMinutes(-5), 1.0, 10),
            countersEnd: new(After, 1.0, 20));

        Assert.Null(energy.CounterUsed);
    }

    [Fact]
    public void NothingReported_GivesNothing()
    {
        Assert.Equal(TripEnergy.None, Compute(10));
    }
}
