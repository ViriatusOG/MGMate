using MGMate.Core.Models;
using MGMate.Data;
using MGMate.Data.Repositories;
using MGMate.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MGMate.Tests;

public class ChargeRecorderTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FinishedCharges_AreSavedOnce_AndTheirChartAndNotesWork()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var vehicle = new Vehicle { Vin = "FAKEVN00000000001" };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        var telemetry = new TelemetryRepository(db);
        var charges = new ChargeRepository(db, telemetry);
        var recorder = new ChargeRecorder(telemetry, charges, NullLogger.Instance);

        db.TelemetrySnapshots.AddRange(
            new TelemetrySnapshot
            {
                VehicleId = vehicle.Id,
                RecordedAt = T0,
                IsCharging = true,
                EvSocPercent = 50,
                HvPower = -7,
            },
            new TelemetrySnapshot
            {
                VehicleId = vehicle.Id,
                RecordedAt = T0.AddMinutes(10),
                IsCharging = true,
                EvSocPercent = 52,
                HvPower = -7,
            },
            new TelemetrySnapshot
            {
                VehicleId = vehicle.Id,
                RecordedAt = T0.AddMinutes(15),
                IsCharging = false,
                EvSocPercent = 53,
            },
            // A second charge still going at the time of the run.
            new TelemetrySnapshot
            {
                VehicleId = vehicle.Id,
                RecordedAt = T0.AddHours(5),
                IsCharging = true,
                EvSocPercent = 53,
                HvPower = -7,
            });
        await db.SaveChangesAsync(ct);

        var now = T0.AddHours(5).AddMinutes(5);
        Assert.Equal(1, await recorder.RecordAsync(vehicle.Id, now, ct));
        Assert.Equal(0, await recorder.RecordAsync(vehicle.Id, now, ct));

        var log = await charges.GetLogAsync(vehicle.Id, T0.AddDays(-1), T0.AddDays(1), ct);
        var charge = Assert.Single(log);
        Assert.Equal((50.0, 53.0), (charge.SocStartPercent!.Value, charge.SocEndPercent!.Value));

        Assert.NotEmpty(await charges.GetChartAsync(vehicle.Id, charge.Id, ct));
        var edited = (await charges.UpdateAsync(vehicle.Id, charge.Id, new ChargeEdit("Supermarket", "public", 12.5m), ct))!;
        Assert.Equal("Supermarket", edited.Notes);
        Assert.Equal("public", edited.Place);
        Assert.Equal(12.5m, edited.PaidAmount);
        Assert.True((await charges.SetHiddenAsync(vehicle.Id, charge.Id, true, ct))!.IsHidden);

        // Once the second charge has stopped for long enough, it is saved too and can be joined.
        Assert.Equal(1, await recorder.RecordAsync(vehicle.Id, T0.AddHours(7), ct));
        var merged = await charges.MergeWithNextAsync(vehicle.Id, charge.Id, ct);
        Assert.Equal(ChargeMergeOutcome.Merged, merged.Outcome);
        Assert.Single(await charges.GetLogAsync(vehicle.Id, T0.AddDays(-1), T0.AddDays(1), ct));
    }

    // Pre-heating after a home charge finished, still plugged in, is not charging power on its chart.
    [Fact]
    public async Task TheChart_ShowsChargingPowerOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var vehicle = new Vehicle { Vin = "FAKEVN00000000001" };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        db.TelemetrySnapshots.AddRange(
            new TelemetrySnapshot
            {
                VehicleId = vehicle.Id,
                RecordedAt = T0,
                IsCharging = true,
                EvSocPercent = 50,
                HvPower = -5.5,
                ChargingType = "AC",
            },
            new TelemetrySnapshot
            {
                VehicleId = vehicle.Id,
                RecordedAt = T0.AddMinutes(5),
                IsCharging = true,
                EvSocPercent = 51,
                HvPower = -30,
            },
            new TelemetrySnapshot
            {
                VehicleId = vehicle.Id,
                RecordedAt = T0.AddMinutes(10),
                IsCharging = true,
                EvSocPercent = 52,
                HvPower = -5.5,
            },
            new TelemetrySnapshot
            {
                VehicleId = vehicle.Id,
                RecordedAt = T0.AddMinutes(15),
                IsCharging = false,
                EvSocPercent = 52,
                HvPower = 40,
            });
        db.ChargeSessions.Add(new ChargeSession
        {
            VehicleId = vehicle.Id,
            StartedAt = T0,
            EndedAt = T0.AddMinutes(10),
            MaxPowerKw = 5.5,
            ChargeType = "AC",
        });
        await db.SaveChangesAsync(ct);

        var charges = new ChargeRepository(db, new TelemetryRepository(db));
        var id = Assert.Single(await charges.GetLogAsync(vehicle.Id, T0.AddDays(-1), T0.AddDays(1), ct)).Id;

        var chart = await charges.GetChartAsync(vehicle.Id, id, ct);

        Assert.Equal(new double?[] { 5.5, null, 5.5, 0 }, chart.Select(p => p.PowerKw).ToArray());
        Assert.Equal(52, chart[^1].SocPercent);
    }

    // A charge saved before the charger's energy was recorded gets it from its own readings.
    [Fact]
    public async Task AnEarlierCharge_GetsItsChargerEnergyFilledIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var vehicle = new Vehicle { Vin = "FAKEVN00000000001" };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        db.TelemetrySnapshots.AddRange(Enumerable.Range(0, 13).Select(i => new TelemetrySnapshot
        {
            VehicleId = vehicle.Id,
            RecordedAt = T0.AddMinutes(i * 5),
            IsCharging = true,
            EvSocPercent = 40 + i,
            HvPower = -7,
            ObcPowerSinglePhase = 7700,
            ObcPowerThreePhase = 2567,
        }));
        db.ChargeSessions.Add(new ChargeSession
        {
            VehicleId = vehicle.Id,
            StartedAt = T0,
            EndedAt = T0.AddHours(1),
            EnergyAddedKwh = 7,
            ChargeType = "AC",
        });
        await db.SaveChangesAsync(ct);

        var telemetry = new TelemetryRepository(db);
        var charges = new ChargeRepository(db, telemetry);
        var recorder = new ChargeRecorder(telemetry, charges, NullLogger.Instance);

        Assert.Equal(1, await recorder.BackfillChargerEnergyAsync(vehicle.Id, ct));
        var charge = Assert.Single(await charges.GetLogAsync(vehicle.Id, T0.AddDays(-1), T0.AddDays(1), ct));
        Assert.Equal(7.7, charge.ChargerEnergyKwh!.Value, 2);
        Assert.Equal(0, await recorder.BackfillChargerEnergyAsync(vehicle.Id, ct));
    }
}
