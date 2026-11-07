using GarageStack.Core.Models;
using GarageStack.Data;
using GarageStack.Data.Repositories;
using GarageStack.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GarageStack.Tests;

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
            new TelemetrySnapshot { VehicleId = vehicle.Id, RecordedAt = T0, IsCharging = true, EvSocPercent = 50, HvPower = -7 },
            new TelemetrySnapshot { VehicleId = vehicle.Id, RecordedAt = T0.AddMinutes(10), IsCharging = true, EvSocPercent = 52, HvPower = -7 },
            new TelemetrySnapshot { VehicleId = vehicle.Id, RecordedAt = T0.AddMinutes(15), IsCharging = false, EvSocPercent = 53 },
            // A second charge still going at the time of the run.
            new TelemetrySnapshot { VehicleId = vehicle.Id, RecordedAt = T0.AddHours(5), IsCharging = true, EvSocPercent = 53, HvPower = -7 });
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
}
