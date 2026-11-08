using MGMate.Core.Models;
using MGMate.Data;
using MGMate.Data.Repositories;
using MGMate.Worker.Services;
using Microsoft.EntityFrameworkCore;

namespace MGMate.Tests;

public class TripEnergyReaderTests
{
    private static readonly DateTime T0 = new(2026, 11, 7, 8, 0, 0, DateTimeKind.Utc);

    // Cold costs range, so a trip keeps the outside temperature it was driven in: the mean of what
    // the car reported while driving, nothing from before or after.
    [Fact]
    public async Task ATrip_GetsTheMeanOutsideTemperatureItWasDrivenIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var vehicle = new Vehicle { Vin = "FAKEVN00000000001" };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        db.TelemetrySnapshots.AddRange(
            new TelemetrySnapshot { VehicleId = vehicle.Id, RecordedAt = T0.AddMinutes(-30), ExteriorTemperature = 20 },
            new TelemetrySnapshot { VehicleId = vehicle.Id, RecordedAt = T0.AddMinutes(5), ExteriorTemperature = 4 },
            new TelemetrySnapshot { VehicleId = vehicle.Id, RecordedAt = T0.AddMinutes(15), ExteriorTemperature = 6 },
            new TelemetrySnapshot { VehicleId = vehicle.Id, RecordedAt = T0.AddMinutes(25), ExteriorTemperature = 8 },
            new TelemetrySnapshot { VehicleId = vehicle.Id, RecordedAt = T0.AddHours(3), ExteriorTemperature = 15 });
        await db.SaveChangesAsync(ct);

        var energy = await new TripEnergyReader(new TelemetryRepository(db))
            .ReadAsync(vehicle.Id, T0, T0.AddMinutes(30), 20, ct);

        Assert.Equal(6, energy.OutsideTemperature);
    }
}
