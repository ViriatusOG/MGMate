namespace GarageStack.Core.Models;

/// <summary>
/// What a trip took out of the traction battery, as the car reported it at either end. Each figure
/// is null when the car did not report it, or reported it in a way that cannot be trusted for this
/// one trip.
/// <para>
/// <see cref="CounterUsed"/> is the car's own day counter (powerUsageOfDay) over the trip, in the
/// counter's unit: kWh on a plug-in car, hundredths of a litre on a plain hybrid, as
/// <see cref="DailyStat.CounterTotal"/>. The state of charge at either end is the fallback a client
/// turns into kWh with the battery's capacity, which only it knows for sure.
/// </para>
/// </summary>
public sealed record TripEnergy(double? SocStartPercent, double? SocEndPercent, double? CounterUsed)
{
    public static readonly TripEnergy None = new(null, null, null);
}

/// <summary>A single value the car reported, and when it was stored.</summary>
public sealed record TimedReading(DateTime RecordedAt, double Value);

/// <summary>
/// The car's day counters as one poll reported them: the energy (or fuel) used today and the
/// distance driven today. Both reset when the car's day ends.
/// </summary>
public sealed record DayCounterReading(DateTime RecordedAt, double Used, double? MileageKm);
