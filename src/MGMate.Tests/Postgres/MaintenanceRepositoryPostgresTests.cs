using System.Text.Json;
using MGMate.Api.Endpoints;
using MGMate.Core.Models;
using MGMate.Data.Repositories;

namespace MGMate.Tests;

// The service form sends a plain day. The in-memory provider stores any DateTime, so only a real
// timestamp with time zone column shows that one of unspecified kind is refused.
public class MaintenanceRepositoryPostgresTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task LogService_WithAPlainDayFromTheForm_IsSaved()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgresDatabase.CreateAsync(ct);
        var req = JsonSerializer.Deserialize<LogMaintenanceServiceRequest>(
            """{"performedAt":"2026-11-07","odometerKm":24.14,"notes":null}""", Web)!;

        await using var db = database.CreateContext();
        db.Vehicles.Add(new Vehicle { Vin = "FAKEVN00000000001" });
        await db.SaveChangesAsync(ct);
        var vehicleId = db.Vehicles.Single().Id;
        var repo = new MaintenanceRepository(db);
        var item = await repo.AddAsync(new MaintenanceItem
        {
            VehicleId = vehicleId, Name = "Annual service", IntervalKm = 24_140, IntervalMonths = 12,
            LastServiceDate = MaintenanceEndpoints.AsUtc((DateTime?)req.PerformedAt),
        }, ct);

        var logged = await repo.LogServiceAsync(vehicleId, item.Id, new MaintenanceLogEntry
        {
            PerformedAt = MaintenanceEndpoints.AsUtc(req.PerformedAt),
            OdometerKm = req.OdometerKm,
            Notes = req.Notes,
        }, ct);

        var expected = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
        Assert.NotNull(logged);
        Assert.Equal(expected, logged.Item.LastServiceDate);
        await using var check = database.CreateContext();
        Assert.Equal(expected, (await new MaintenanceRepository(check).GetLogAsync(vehicleId, item.Id, ct))!.Single().PerformedAt);
    }
}
