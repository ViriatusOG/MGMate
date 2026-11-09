using MGMate.Api.Endpoints;
using MGMate.Core.Helpers;
using MGMate.Core.Models;

namespace MGMate.Tests;

// A new car has no service yet: its items count from the first registration, at 0 km.
public class MaintenanceBaselineTests
{
    private static readonly DateTime Registered = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);

    private static MaintenanceItem Annual(DateTime? lastServiceDate = null, double? lastServiceOdometerKm = null) => new()
    {
        Name = "Annual service",
        IntervalKm = 24_140,
        IntervalMonths = 12,
        LastServiceDate = lastServiceDate,
        LastServiceOdometerKm = lastServiceOdometerKm,
    };

    [Fact]
    public void NoServiceAndNoRegistration_HasNothingToCountFrom()
    {
        var baseline = MaintenanceDueCalculator.BaselineFor(Annual(), new Vehicle());

        Assert.Equal(MaintenanceBaselineSource.None, baseline.Source);
        Assert.Null(baseline.ApiName);
        Assert.Equal(MaintenanceDueStatus.Unknown, MaintenanceDueCalculator.Calculate(Annual(), new Vehicle(), 24, Now).Status);
    }

    [Fact]
    public void NoService_CountsFromRegistrationAtZeroKm()
    {
        var vehicle = new Vehicle { RegistrationDate = Registered };

        var baseline = MaintenanceDueCalculator.BaselineFor(Annual(), vehicle);
        var due = MaintenanceDueCalculator.Calculate(Annual(), vehicle, 24, Now);

        Assert.Equal(MaintenanceBaselineSource.Registration, baseline.Source);
        Assert.Equal("registration", baseline.ApiName);
        Assert.Equal(MaintenanceDueStatus.Ok, due.Status);
        Assert.Equal(new DateTime(2027, 7, 31, 0, 0, 0, DateTimeKind.Utc), due.NextDueDate);
        Assert.Equal(24_140, due.NextDueOdometerKm);
    }

    [Fact]
    public void ALoggedService_WinsOverRegistration()
    {
        var serviced = new DateTime(2027, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var vehicle = new Vehicle { RegistrationDate = Registered };

        var baseline = MaintenanceDueCalculator.BaselineFor(Annual(serviced, 20_000), vehicle);

        Assert.Equal(MaintenanceBaselineSource.Service, baseline.Source);
        Assert.Equal(serviced, baseline.Date);
        Assert.Equal(20_000, baseline.OdometerKm);
    }

    [Fact]
    public void ItemDto_SaysWhatItCountsFrom()
    {
        var dto = MaintenanceEndpoints.ToDto(Annual(), new Vehicle { RegistrationDate = Registered }, 24);

        Assert.Equal("registration", dto.CountingFrom);
        Assert.Equal(Registered, dto.CountingFromDate);
        Assert.Equal("ok", dto.DueStatus);
        Assert.Null(dto.LastServiceDate);
    }
}
