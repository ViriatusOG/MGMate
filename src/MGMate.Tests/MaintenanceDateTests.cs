using System.Text.Json;
using MGMate.Api.Endpoints;

namespace MGMate.Tests;

// A service date reaches the database as UTC whichever way the request wrote it.
public class MaintenanceDateTests
{
    [Fact]
    public void APlainDay_BindsWithNoKind_AndIsTakenAsMidnightUtc()
    {
        var req = JsonSerializer.Deserialize<LogMaintenanceServiceRequest>(
            """{"performedAt":"2026-11-07"}""", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal(DateTimeKind.Unspecified, req.PerformedAt.Kind);
        var utc = MaintenanceEndpoints.AsUtc(req.PerformedAt);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void AUtcTime_IsKept()
    {
        var value = new DateTime(2026, 11, 7, 9, 30, 0, DateTimeKind.Utc);
        Assert.Equal(value, MaintenanceEndpoints.AsUtc(value));
    }

    [Fact]
    public void ALocalTime_IsConvertedToUtc()
    {
        var local = new DateTime(2026, 11, 7, 9, 30, 0, DateTimeKind.Local);
        var utc = MaintenanceEndpoints.AsUtc(local);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(local.ToUniversalTime(), utc);
    }

    [Fact]
    public void NoDate_StaysEmpty()
    {
        Assert.Null(MaintenanceEndpoints.AsUtc((DateTime?)null));
    }
}
