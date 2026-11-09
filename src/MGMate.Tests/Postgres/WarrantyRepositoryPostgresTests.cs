using MGMate.Api.Endpoints;
using MGMate.Core.Helpers;
using MGMate.Core.Models;
using MGMate.Data.Repositories;

namespace MGMate.Tests;

// The dates arrive as plain days. Npgsql refuses a DateTime of unspecified kind for a timestamptz
// column, which the in-memory provider never notices, so these run on a real database.
public class WarrantyRepositoryPostgresTests
{
    [Fact]
    public async Task OwnershipDates_AndDefaultCovers_SaveAndReadBack()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgresDatabase.CreateAsync(ct);
        int vehicleId;
        await using (var db = database.CreateContext())
        {
            var vehicle = new Vehicle { Vin = "FAKEVN00000000001" };
            db.Vehicles.Add(vehicle);
            await db.SaveChangesAsync(ct);
            vehicleId = vehicle.Id;

            await new VehicleRepository(db).SetOwnershipDatesAsync(vehicleId,
                WarrantyEndpoints.AsUtcDay(new DateOnly(2026, 7, 31)),
                WarrantyEndpoints.AsUtcDay(new DateOnly(2026, 11, 7)), 24.1, ct);
            await new WarrantyRepository(db).ReplaceAllAsync(vehicleId, WarrantyDefaults.For(vehicleId), ct);
        }

        await using var check = database.CreateContext();
        var saved = await new VehicleRepository(check).GetByVinAsync("FAKEVN00000000001", ct);
        Assert.Equal(new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc), saved!.RegistrationDate);
        Assert.Equal(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), saved.DeliveryDate);
        Assert.Equal(24.1, saved.DeliveryOdometerKm);

        var repo = new WarrantyRepository(check);
        var covers = await repo.ListAsync(vehicleId, ct);
        Assert.Equal(WarrantyDefaults.For(vehicleId).Select(c => c.Name), covers.Select(c => c.Name));

        await repo.ReplaceAllAsync(vehicleId, [new WarrantyCover { VehicleId = vehicleId, Name = "Extended", Months = 120 }], ct);
        Assert.Equal("Extended", Assert.Single(await repo.ListAsync(vehicleId, ct)).Name);
    }
}
