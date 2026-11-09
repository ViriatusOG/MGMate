using MGMate.Core.Helpers;

namespace MGMate.Tests;

public class WarrantyCalculatorTests
{
    private static readonly DateTime Registered = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = new(2026, 11, 9, 21, 0, 0, DateTimeKind.Utc);
    private const double Mile = 1.609344;

    [Fact]
    public void ThreeYearCover_IsActive_EndingOnTheRegistrationAnniversary()
    {
        var r = WarrantyCalculator.Calculate(36, 60_000 * Mile, Registered, 58 * Mile, Today);

        Assert.Equal(WarrantyStatus.Active, r.Status);
        Assert.Equal(new DateTime(2029, 7, 31, 0, 0, 0, DateTimeKind.Utc), r.EndsOn);
        Assert.Equal(59_942, r.KmRemaining!.Value / Mile, 3);
        Assert.Null(r.EndedBy);
    }

    [Fact]
    public void ThreeMonthCover_HasExpiredByTime()
    {
        var r = WarrantyCalculator.Calculate(3, 1_000 * Mile, Registered, 58 * Mile, Today);

        Assert.Equal(WarrantyStatus.Expired, r.Status);
        Assert.Equal(new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc), r.EndsOn);
        Assert.Equal("time", r.EndedBy);
    }

    [Fact]
    public void MileageLimit_IsTheOdometerReading_NotDistanceSinceRegistration()
    {
        var r = WarrantyCalculator.Calculate(12, 15_000 * Mile, Registered, 15_001 * Mile, Today);

        Assert.Equal(WarrantyStatus.Expired, r.Status);
        Assert.Equal("distance", r.EndedBy);
    }

    [Fact]
    public void LastThirtyDays_AreEndingSoon()
    {
        var r = WarrantyCalculator.Calculate(6, null, Registered, null, new DateTime(2027, 1, 10, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(WarrantyStatus.EndingSoon, r.Status);
        Assert.Equal(21, r.DaysRemaining);
    }

    [Fact]
    public void LastTenthOfTheMiles_IsEndingSoon()
    {
        var r = WarrantyCalculator.Calculate(36, 45_000 * Mile, Registered, 41_000 * Mile, Today);

        Assert.Equal(WarrantyStatus.EndingSoon, r.Status);
    }

    [Fact]
    public void ZeroMonths_IsNotCovered()
    {
        Assert.Equal(WarrantyStatus.NotCovered, WarrantyCalculator.Calculate(0, null, Registered, 100, Today).Status);
    }

    [Fact]
    public void WithoutARegistrationDate_NothingIsWorkedOut()
    {
        var r = WarrantyCalculator.Calculate(84, 80_000 * Mile, null, 100, Today);

        Assert.Equal(WarrantyStatus.Unknown, r.Status);
        Assert.Null(r.EndsOn);
    }

    [Fact]
    public void WithoutAnOdometerReading_TimeAloneDecides()
    {
        var r = WarrantyCalculator.Calculate(84, 80_000 * Mile, Registered, null, Today);

        Assert.Equal(WarrantyStatus.Active, r.Status);
        Assert.Null(r.KmRemaining);
    }

    [Fact]
    public void Defaults_FollowTheMgUkStatement()
    {
        var covers = WarrantyDefaults.For(7);

        var vehicle = covers.Single(c => c.Name == "Vehicle");
        Assert.Equal(84, vehicle.Months);
        Assert.Equal(80_000, vehicle.DistanceKm!.Value / Mile, 6);
        Assert.True(vehicle.Featured);
        var battery = covers.Single(c => c.Name == "High voltage battery");
        Assert.Equal(96, battery.Months);
        Assert.Equal(100_000, battery.DistanceKm!.Value / Mile, 6);
        Assert.Equal(0, covers.Single(c => c.Name == "Tyres").Months);
        Assert.All(covers, c => Assert.Equal(7, c.VehicleId));
        Assert.Equal(covers.Count, covers.Select(c => c.SortOrder).Distinct().Count());
    }
}
