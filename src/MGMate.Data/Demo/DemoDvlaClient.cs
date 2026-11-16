using System.Text.Json;
using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;
using MGMate.Core.Models;

namespace MGMate.Data.Demo;

/// <summary>
/// The DVLA as the demo car sees it, whatever plate is entered: taxed for another eight months and
/// too new for an MOT. Nothing leaves the machine.
/// </summary>
public sealed class DemoDvlaClient(TimeProvider time) : IDvlaClient
{
    public const int TaxDaysLeft = 250;

    public static DvlaVehicle Vehicle(DateTime utcNow)
    {
        var taxDue = ZoneTime.DateOf(utcNow, DvlaReminder.UkZone).AddDays(TaxDaysLeft);
        var record = new Dictionary<string, object?>
        {
            ["registrationNumber"] = "DEMO",
            ["taxStatus"] = "Taxed",
            ["taxDueDate"] = taxDue.ToString("yyyy-MM-dd"),
            ["motStatus"] = "No details held by DVLA",
            ["make"] = "MG",
            ["colour"] = "WHITE",
            ["fuelType"] = "ELECTRICITY",
            ["yearOfManufacture"] = 2026,
            ["monthOfFirstRegistration"] = "2026-05",
            ["co2Emissions"] = 0,
            ["typeApproval"] = "M1",
            ["wheelplan"] = "2 AXLE RIGID BODY",
            ["dateOfLastV5CIssued"] = "2026-05-18",
            ["markedForExport"] = false,
        };
        return new DvlaVehicle("Taxed", taxDue, "No details held by DVLA", null, "MG", "WHITE", "ELECTRICITY",
            JsonSerializer.Serialize(record));
    }

    public Task<DvlaVehicle?> GetVehicleAsync(string plate, CancellationToken ct = default) =>
        Task.FromResult<DvlaVehicle?>(Vehicle(time.GetUtcNow().UtcDateTime));
}
