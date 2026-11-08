using MGMate.Core.Models;

namespace MGMate.Tests;

public class TripTelemetryPointTests
{
    private static List<TripTelemetryPoint> Points(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new TripTelemetryPoint(DateTime.UnixEpoch.AddSeconds(i), i, null, null, null, null, null))
            .ToList();

    [Fact]
    public void Thin_LeavesAShortTripAsItIs()
    {
        var points = Points(10);
        Assert.Same(points, TripTelemetryPoint.Thin(points));
    }

    [Fact]
    public void Thin_KeepsBothEndsOfALongTrip()
    {
        var thinned = TripTelemetryPoint.Thin(Points(5000));
        Assert.Equal(TripTelemetryPoint.MaxPoints, thinned.Count);
        Assert.Equal(0, thinned[0].Speed);
        Assert.Equal(4999, thinned[^1].Speed);
    }
}
