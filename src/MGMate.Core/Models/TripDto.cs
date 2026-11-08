using System.Text.Json.Serialization;
using MGMate.Core.Helpers;

namespace MGMate.Core.Models;

/// <summary>
/// A position on a trip's path. <paramref name="Source"/> is null for a fix the car reported as it
/// drove, and otherwise says why the point is there: left out of the JSON when null, so the trips
/// saved before it existed read the same.
/// </summary>
public record TripPoint(
    DateTime RecordedAt,
    double Latitude,
    double Longitude,
    double? Speed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TripPointSource? Source = null);

/// <summary>
/// One reading taken while a trip was driven, for its detail: speed in km/h, charge level in
/// percent, battery power in kW (either sign: which way is charging differs between cars),
/// elevation in metres, outside temperature in °C, whether the climate was on and the electric
/// range the car estimated, in km.
/// </summary>
public sealed record TripTelemetryPoint(
    DateTime RecordedAt,
    double? Speed,
    double? SocPercent,
    double? PowerKw,
    double? Elevation,
    double? OutsideTemperature,
    bool? ClimateOn,
    double? RangeKm = null)
{
    /// <summary>The most points a trip's detail is sent; a longer trip is thinned evenly to this.</summary>
    public const int MaxPoints = 600;

    public static IReadOnlyList<TripTelemetryPoint> Thin(IReadOnlyList<TripTelemetryPoint> points)
    {
        if (points.Count <= MaxPoints) return points;
        var step = (double)(points.Count - 1) / (MaxPoints - 1);
        return Enumerable.Range(0, MaxPoints).Select(i => points[(int)Math.Round(i * step)]).ToList();
    }
}

/// <summary>Where a trip point came from, when the car did not report it while driving.</summary>
[JsonConverter(typeof(TripPointSourceJsonConverter))]
public enum TripPointSource
{
    /// <summary>A parked position the car reported, added where the trip left or reached it.</summary>
    [JsonStringEnumMemberName("anchored")]
    Anchored,

    /// <summary>Placed by someone editing the trip.</summary>
    [JsonStringEnumMemberName("manual")]
    Manual,
}

/// <summary>Reads and writes a source by name only, as <see cref="TripPurposeJsonConverter"/> does.</summary>
public sealed class TripPointSourceJsonConverter()
    : JsonStringEnumConverter<TripPointSource>(namingPolicy: null, allowIntegerValues: false);

/// <summary>
/// A trip as the Api serves it. <paramref name="Id"/> is the saved trip's id, or null for a trip
/// the Worker has not saved yet, such as the one being driven. The summary figures are read off
/// <paramref name="Points"/>, so a client that needs only those can ask for
/// <see cref="TripSummaryDto"/> instead and skip the fixes.
/// </summary>
public record TripDto(int Index, DateTime StartedAt, DateTime EndedAt, double DistanceKm, int PointCount, IReadOnlyList<TripPoint> Points, long? Id = null)
{
    /// <summary>Where the trip ended, or null for a trip without fixes.</summary>
    public double? EndLatitude => Points.Count > 0 ? Points[^1].Latitude : null;

    /// <inheritdoc cref="EndLatitude"/>
    public double? EndLongitude => Points.Count > 0 ? Points[^1].Longitude : null;

    /// <summary>The highest speed any fix reported, or null when none carried a speed.</summary>
    public double? MaxSpeedKmh => Points.Max(p => p.Speed);

    /// <summary>The mean of the speeds reported while moving, or null when none was.</summary>
    public double? AvgMovingSpeedKmh => MovingSpeedSamples > 0 ? MovingSpeeds.Average() : null;

    /// <summary>
    /// How many fixes reported a speed while moving: the weight to give
    /// <see cref="AvgMovingSpeedKmh"/> when averaging over several trips.
    /// </summary>
    public int MovingSpeedSamples => MovingSpeeds.Count();

    private IEnumerable<double> MovingSpeeds => Points.Where(p => p.Speed > 0).Select(p => p.Speed!.Value);

    /// <summary>The same trip without its fixes.</summary>
    public TripSummaryDto ToSummary() => new(
        Index, Id, StartedAt, EndedAt, DistanceKm, PointCount,
        EndLatitude, EndLongitude, MaxSpeedKmh, AvgMovingSpeedKmh, MovingSpeedSamples);

    /// <summary>The same trip with its fixes packed into three arrays; see <see cref="CompactTripDto"/>.</summary>
    public CompactTripDto ToCompact()
    {
        // Between rounded offsets, not rounded after subtracting, as the polyline does, so the
        // running total stays within half a millisecond of every fix instead of drifting.
        var intervals = new long[Points.Count];
        long previous = 0;
        for (var i = 0; i < Points.Count; i++)
        {
            var offset = (long)Math.Round((Points[i].RecordedAt - StartedAt).TotalMilliseconds);
            intervals[i] = offset - previous;
            previous = offset;
        }

        return new(
            Index, Id, StartedAt, EndedAt, DistanceKm, PointCount,
            EndLatitude, EndLongitude, MaxSpeedKmh, AvgMovingSpeedKmh, MovingSpeedSamples,
            PolylineCodec.Encode([.. Points.Select(p => new GeoCoordinate(p.Latitude, p.Longitude))]),
            intervals,
            [.. Points.Select(p => p.Speed)],
            Points.Any(p => p.Source is not null) ? [.. Points.Select(p => p.Source)] : null);
    }
}

/// <summary>
/// A trip without its fixes: what the statistics page needs from a whole period of trips, at a
/// fraction of the size.
/// </summary>
public record TripSummaryDto(
    int Index,
    long? Id,
    DateTime StartedAt,
    DateTime EndedAt,
    double DistanceKm,
    int PointCount,
    double? EndLatitude,
    double? EndLongitude,
    double? MaxSpeedKmh,
    double? AvgMovingSpeedKmh,
    int MovingSpeedSamples);

/// <summary>
/// A trip as <see cref="TripDto"/> serves it, with its fixes as three arrays instead of an object
/// each: about half the size gzipped, for a map that shows up to 90 days of them. Opt-in
/// (<c>?format=compact</c>), so a script reading the plain format is not affected.
/// <para>
/// Fix <c>i</c> is at <see cref="Line"/>'s vertex <c>i</c> (an encoded polyline at
/// <see cref="PolylineCodec.DefaultPrecision"/> decimals, about 10 cm) and reported
/// <see cref="Speeds"/>[i]. <see cref="IntervalsMs"/>[i] is the milliseconds since the fix before
/// it, or since <see cref="StartedAt"/> for the first, so a fix's time is their running total.
/// <see cref="Sources"/>[i] is its <see cref="TripPoint.Source"/>; the array is left out when every
/// point is a fix the car reported while driving, as almost every trip's are.
/// </para>
/// </summary>
public record CompactTripDto(
    int Index,
    long? Id,
    DateTime StartedAt,
    DateTime EndedAt,
    double DistanceKm,
    int PointCount,
    double? EndLatitude,
    double? EndLongitude,
    double? MaxSpeedKmh,
    double? AvgMovingSpeedKmh,
    int MovingSpeedSamples,
    string Line,
    IReadOnlyList<long> IntervalsMs,
    IReadOnlyList<double?> Speeds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<TripPointSource?>? Sources = null);
