using GarageStack.Core.Helpers;
using GarageStack.Core.Models;

namespace GarageStack.Tests;

public class ChargeSegmenterTests
{
    private static readonly DateTime T0 = new(2026, 11, 7, 23, 0, 0, DateTimeKind.Utc);

    private static ChargeSample S(int minutes, bool? charging, double? soc = null, double? powerKw = null, string? type = null) =>
        new(T0.AddMinutes(minutes), charging, soc, powerKw, type, 51.7, -1.2, 5000, null, 80);

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
