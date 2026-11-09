using MGMate.Api.Endpoints;

namespace MGMate.Tests;

// Each refusal names the rule that failed: the frontend translates that code for the user.
public class WarrantyValidationTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 21, 0, 0, DateTimeKind.Utc);

    private static string? CoverRule(string name, int months, double? distanceKm, string? notes = null) =>
        WarrantyEndpoints.ValidateCover(new WarrantyCoverRequest(name, notes, months, distanceKm, false))?.Code;

    private static string? DatesRule(DateOnly? registration, DateOnly? delivery = null, double? odometer = null) =>
        WarrantyEndpoints.ValidateDates(new OwnershipDatesRequest(registration, delivery, odometer), Now)?.Code;

    [Fact]
    public void Cover_Valid() => Assert.Null(CoverRule("Paint", 36, 96_560));

    [Fact]
    public void Cover_NotCoveredHasZeroMonths() => Assert.Null(CoverRule("Tyres", 0, null));

    [Theory]
    [InlineData("", 12, null, "warranty.nameRequired")]
    [InlineData("Paint", -1, null, "warranty.monthsOutOfRange")]
    [InlineData("Paint", 361, null, "warranty.monthsOutOfRange")]
    [InlineData("Paint", 12, 0d, "warranty.distanceOutOfRange")]
    public void Cover_Refused(string name, int months, double? km, string rule) =>
        Assert.Equal(rule, CoverRule(name, months, km));

    [Fact]
    public void Cover_LongNotesRefused() =>
        Assert.Equal("warranty.notesTooLong", CoverRule("Paint", 12, null, new string('x', 501)));

    [Fact]
    public void Dates_Valid() => Assert.Null(DatesRule(new DateOnly(2026, 7, 31), new DateOnly(2026, 11, 7), 24));

    [Fact]
    public void Dates_AllCleared() => Assert.Null(DatesRule(null));

    [Fact]
    public void Dates_FutureRegistrationRefused() =>
        Assert.Equal("warranty.registrationDateOutOfRange", DatesRule(new DateOnly(2027, 1, 1)));

    [Fact]
    public void Dates_DeliveryBeforeRegistrationRefused() =>
        Assert.Equal("warranty.deliveryBeforeRegistration", DatesRule(new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 1)));

    [Fact]
    public void Dates_NegativeOdometerRefused() =>
        Assert.Equal("warranty.odometerOutOfRange", DatesRule(null, null, -1));

    [Fact]
    public void AsUtcDay_IsMidnightUtc()
    {
        var day = WarrantyEndpoints.AsUtcDay(new DateOnly(2026, 7, 31));
        Assert.Equal(new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc), day);
        Assert.Equal(DateTimeKind.Utc, day!.Value.Kind);
    }
}
