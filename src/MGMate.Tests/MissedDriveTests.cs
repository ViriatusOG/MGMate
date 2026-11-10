using MGMate.Core.Helpers;
using MGMate.Core.Models;

namespace MGMate.Tests;

public class MissedDriveTests
{
    private static readonly DateTime T0 = new(2026, 11, 9, 23, 40, 0, DateTimeKind.Utc);

    private static TripPoint Fix(double minutes, double lat, double lon, double? speed = 0) =>
        new(T0.AddMinutes(minutes), lat, lon, speed);

    [Fact]
    public void Candidates_PairsTwoParkedFixesInARowWithNoTripBetweenThem()
    {
        var home = Fix(0, 51.50, -0.42);
        var work = Fix(410, 51.43, -0.96);

        var pairs = MissedDrive.Candidates([home, work], []);

        Assert.Equal((home, work), Assert.Single(pairs));
    }

    [Fact]
    public void Candidates_LeavesOutStretchesASavedTripCovers()
    {
        var fixes = new[] { Fix(0, 51.50, -0.42), Fix(410, 51.43, -0.96) };
        var trip = (T0.AddMinutes(100), T0.AddMinutes(140));

        Assert.Empty(MissedDrive.Candidates(fixes, [trip]));
    }

    [Fact]
    public void Candidates_LeavesOutAMissedDriveAlreadySaved()
    {
        // Saved at the second fix, so the next run sees it there and leaves the pair alone.
        var fixes = new[] { Fix(0, 51.50, -0.42), Fix(410, 51.43, -0.96) };
        var saved = (T0.AddMinutes(410), T0.AddMinutes(410));

        Assert.Empty(MissedDrive.Candidates(fixes, [saved]));
    }

    [Fact]
    public void Candidates_FindsTheDriveBackAfterAMissedDriveSavedAtTheSameSpot()
    {
        // Out in the morning and back in the afternoon, neither followed: the morning drive is saved
        // at the work fix, which is also where the drive home starts.
        var home = Fix(0, 51.50, -0.42);
        var work = Fix(410, 51.43, -0.96);
        var homeAgain = Fix(1050, 51.50, -0.42);
        var morning = (work.RecordedAt, work.RecordedAt);

        var pairs = MissedDrive.Candidates([home, work, homeAgain], [morning]);

        Assert.Equal((work, homeAgain), Assert.Single(pairs));
    }

    [Fact]
    public void Candidates_FindsAMissedDriveRightAfterAFollowedOne()
    {
        var parked = Fix(60, 51.43, -0.96);
        var later = Fix(500, 51.50, -0.42);
        var followed = (T0, parked.RecordedAt);

        var pairs = MissedDrive.Candidates([Fix(0, 51.50, -0.42, speed: 50), parked, later], [followed]);

        Assert.Equal((parked, later), Assert.Single(pairs));
    }

    [Fact]
    public void Candidates_LeavesOutAPairWhoseSecondFixStartsAFollowedTrip()
    {
        var fixes = new[] { Fix(0, 51.50, -0.42), Fix(410, 51.43, -0.96) };
        var next = (T0.AddMinutes(410), T0.AddMinutes(450));

        Assert.Empty(MissedDrive.Candidates(fixes, [next]));
    }

    [Fact]
    public void Candidates_LeavesOutMovingFixesAndPollsTooCloseTogether()
    {
        Assert.Empty(MissedDrive.Candidates([Fix(0, 51.50, -0.42), Fix(60, 51.43, -0.96, speed: 40)], []));
        Assert.Empty(MissedDrive.Candidates([Fix(0, 51.50, -0.42), Fix(2, 51.43, -0.96)], []));
    }

    [Fact]
    public void Build_TakesTheDistanceFromTheOdometerAndEndsWhereTheCarWasFound()
    {
        var home = Fix(0, 51.50, -0.42);
        var work = Fix(410, 51.43, -0.96);

        var trip = MissedDrive.Build(home, work, 323.9, 371.6);

        Assert.NotNull(trip);
        Assert.Equal(47.7, trip.DistanceKm, 2);
        Assert.Equal(work.RecordedAt, trip.StartedAt);
        Assert.Equal(work.RecordedAt, trip.EndedAt);
        Assert.Equal(2, trip.Points.Count);
        Assert.All(trip.Points, p => Assert.Equal(TripPointSource.Anchored, p.Source));
        Assert.Null(trip.MaxSpeedKmh);
    }

    [Fact]
    public void Build_CountsALoopBackToTheSameSpot()
    {
        var trip = MissedDrive.Build(Fix(0, 51.50, -0.42), Fix(90, 51.50, -0.42), 400, 412.5);
        Assert.Equal(12.5, trip!.DistanceKm, 2);
    }

    [Fact]
    public void Build_IgnoresAnOdometerThatHardlyMovedOrIsMissing()
    {
        Assert.Null(MissedDrive.Build(Fix(0, 51.50, -0.42), Fix(90, 51.43, -0.96), 400, 400.4));
        Assert.Null(MissedDrive.Build(Fix(0, 51.50, -0.42), Fix(90, 51.43, -0.96), null, 412));
    }
}
