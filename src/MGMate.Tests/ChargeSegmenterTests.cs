using MGMate.Core.Helpers;
using MGMate.Core.Models;

namespace MGMate.Tests;

public class ChargeSegmenterTests
{
    private static readonly DateTime T0 = new(2026, 11, 7, 23, 0, 0, DateTimeKind.Utc);

    private static ChargeSample S(int minutes, bool? charging, double? soc = null, double? powerKw = null, string? type = null) =>
        new(T0.AddMinutes(minutes), charging, soc, powerKw, type, 51.7, -1.2, 5000, null, 80);

    // An hour at 7 kW into the battery from 7.8 kW at the wall: about 90% efficient.
    [Fact]
    public void AnAcCharge_GetsTheEnergyTheOnBoardChargerDrewFromTheSupply()
    {
        var samples = Enumerable.Range(0, 13)
            .Select(i => S(i * 5, true, 40 + i, -7) with { ObcPowerSinglePhaseW = 7800, ObcPowerThreePhaseW = 2600 })
            .ToList();

        var charge = Assert.Single(ChargeSegmenter.Segment(samples, T0.AddHours(3)).Closed);

        Assert.Equal(7.0, charge.EnergyAddedKwh!.Value, 2);
        Assert.Equal(7.8, charge.ChargerEnergyKwh!.Value, 2);
    }

    // The gateway cannot tell the phases apart; the single-phase figure on a three-phase supply
    // would read three times too high, so the one the battery energy fits is taken.
    [Fact]
    public void TheChargerEnergy_ComesFromWhicheverPhaseFigureTheBatteryEnergyFits()
    {
        var samples = Enumerable.Range(0, 13)
            .Select(i => S(i * 5, true, 40 + i, 10) with { ObcPowerSinglePhaseW = 33000, ObcPowerThreePhaseW = 11000 })
            .ToList();

        Assert.Equal(11.0, ChargeSegmenter.ChargerEnergy(samples, 10)!.Value, 2);
        // Neither fits a battery figure the charger cannot have produced.
        Assert.Null(ChargeSegmenter.ChargerEnergy(samples, 12));
        Assert.Null(ChargeSegmenter.ChargerEnergy(samples, null));
    }

    [Fact]
    public void ADcCharge_HasNoChargerEnergy()
    {
        var samples = Enumerable.Range(0, 7)
            .Select(i => S(i * 5, true, 20 + i * 8, 80, "DC") with { ObcPowerSinglePhaseW = 300 })
            .ToList();

        Assert.Null(Assert.Single(ChargeSegmenter.Segment(samples, T0.AddHours(3)).Closed).ChargerEnergyKwh);
    }

    [Fact]
    public void OneCharge_IsCutWithItsLevelsAndEnergy()
    {
        var samples = new[]
        {
            S(-10, false, soc: 40),
            S(0, true, soc: 40, powerKw: -7),
            S(10, true, soc: 42, powerKw: -7),
            S(20, true, soc: 44, powerKw: -7),
            S(25, false, soc: 45),
        };

        var result = ChargeSegmenter.Segment(samples, T0.AddHours(2));

        var charge = Assert.Single(result.Closed);
        Assert.Null(result.OpenSince);
        Assert.Equal(T0, charge.StartedAt);
        Assert.Equal(T0.AddMinutes(20), charge.EndedAt);
        Assert.Equal((40.0, 45.0), (charge.SocStartPercent!.Value, charge.SocEndPercent!.Value));
        Assert.Equal(7.0 * 20 / 60, charge.EnergyAddedKwh!.Value, 2);
        Assert.Equal(7.0, charge.MaxPowerKw);
        Assert.Equal("AC", charge.ChargeType);
        Assert.Equal(80, charge.SocTargetPercent);
    }

    [Fact]
    public void AShortPause_StaysOneCharge()
    {
        var samples = new[] { S(0, true, powerKw: 7), S(10, false), S(15, true, powerKw: 7) };

        Assert.Single(ChargeSegmenter.Segment(samples, T0.AddHours(2)).Closed);
    }

    [Fact]
    public void ALongPause_MakesTwoCharges()
    {
        var samples = new[] { S(0, true, powerKw: 7), S(5, false), S(60, true, powerKw: 7), S(70, false) };

        Assert.Equal(2, ChargeSegmenter.Segment(samples, T0.AddHours(3)).Closed.Count);
    }

    [Fact]
    public void ACharge_StillGoing_IsLeftOpen()
    {
        var samples = new[] { S(0, true, powerKw: 7), S(10, true, powerKw: 7) };

        var result = ChargeSegmenter.Segment(samples, T0.AddMinutes(15));

        Assert.Empty(result.Closed);
        Assert.Equal(T0, result.OpenSince);
    }

    [Fact]
    public void HighPower_OrADcType_IsDc()
    {
        var fast = new[] { S(0, true, powerKw: -90), S(10, true, powerKw: -85) };
        var typed = new[] { S(0, true, powerKw: 9, type: "DC_CHARGING"), S(10, true, powerKw: 9) };

        Assert.Equal("DC", ChargeSegmenter.Segment(fast, T0.AddHours(1)).Closed[0].ChargeType);
        Assert.Equal("DC", ChargeSegmenter.Segment(typed, T0.AddHours(1)).Closed[0].ChargeType);
    }

    [Fact]
    public void PowerAcrossAGap_IsNotGuessed()
    {
        var samples = new[] { S(0, true, powerKw: 7), S(10, true, powerKw: 7), S(29, true, powerKw: 7) };

        var charge = Assert.Single(ChargeSegmenter.Segment(samples, T0.AddHours(2)).Closed);

        Assert.Equal(7.0 * 10 / 60, charge.EnergyAddedKwh!.Value, 2);
    }
}
