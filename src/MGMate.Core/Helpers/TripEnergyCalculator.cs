using MGMate.Core.Models;

namespace MGMate.Core.Helpers;

/// <summary>
/// Works out a trip's <see cref="TripEnergy"/> from what the car reported just before it set off
/// and once it had parked.
/// <para>
/// The day counters are only trusted when the distance counter moved by the trip's own distance:
/// that proves both readings bracket this trip and nothing else, and that the car's day did not
/// end in between. When the counters reset before the trip (the first drive of the day, read
/// against yesterday's totals), the end reading alone covers the trip, and the same distance check
/// says so.
/// </para>
/// </summary>
public static class TripEnergyCalculator
{
    // GPS distance and the car's own distance never agree exactly: a quarter, and at least a km,
    // still tells one trip from a day of them.
    private const double MinToleranceKm = 1.0;
    private const double RelativeTolerance = 0.25;

    public static TripEnergy Compute(
        DateTime startedAt,
        double distanceKm,
        TimedReading? socStart,
        TimedReading? socEnd,
        DayCounterReading? countersStart,
        DayCounterReading? countersEnd)
    {
        // An end reading from before the trip is only the start reading again: the car sent
        // nothing new, so it says nothing about what the drive used.
        var freshSocEnd = socEnd is not null && socEnd.RecordedAt > startedAt ? socEnd : null;

        return new TripEnergy(
            socStart?.Value,
            freshSocEnd?.Value,
            CounterUsed(startedAt, distanceKm, countersStart, countersEnd));
    }

    private static double? CounterUsed(
        DateTime startedAt, double distanceKm, DayCounterReading? start, DayCounterReading? end)
    {
        if (distanceKm <= 0 || end is not { MileageKm: { } endMileage } || end.RecordedAt <= startedAt)
            return null;

        var tolerance = Math.Max(MinToleranceKm, distanceKm * RelativeTolerance);
        bool Matches(double counted) => Math.Abs(counted - distanceKm) <= tolerance;

        double? used = null;
        if (start is { MileageKm: { } startMileage } && Matches(endMileage - startMileage) && end.Used >= start.Used)
            used = end.Used - start.Used;
        else if (Matches(endMileage))
            used = end.Used;

        // Nothing used over a real distance is a counter that has not caught up yet.
        return used > 0 ? used : null;
    }
}
