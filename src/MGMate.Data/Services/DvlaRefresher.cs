using MGMate.Core.Configuration;
using MGMate.Core.Interfaces;
using MGMate.Core.Models;
using Microsoft.Extensions.Logging;

namespace MGMate.Data.Services;

/// <summary>
/// One DVLA lookup for one vehicle, saved. The API runs it as soon as a plate is entered and the
/// Worker once a lookup is older than DVLA_REFRESH_HOURS, so both write the same row the same way.
/// </summary>
public sealed class DvlaRefresher(
    IDvlaClient client,
    IDvlaRepository repository,
    DvlaOptions options,
    TimeProvider time,
    ILogger<DvlaRefresher> logger)
{
    public bool Enabled => options.Enabled;

    /// <summary>
    /// Whether <paramref name="vehicle"/> is due a lookup: it has a plate, and the last good answer
    /// for that plate is older than the refresh interval (or there is none).
    /// </summary>
    public bool IsDue(Vehicle vehicle, DvlaVehicleInfo? info) =>
        options.Enabled
        && vehicle.LicensePlate is not null
        && (info is not { FetchedAt: { } fetchedAt }
            || info.LicensePlate != vehicle.LicensePlate
            || time.GetUtcNow().UtcDateTime - fetchedAt >= options.RefreshInterval);

    /// <summary>
    /// Asks the DVLA about the vehicle's plate now and saves the answer, or the reason there is
    /// none. Null, having asked nothing, when lookups are off or the vehicle has no plate.
    /// </summary>
    public async Task<DvlaVehicleInfo?> RefreshAsync(Vehicle vehicle, CancellationToken ct = default)
    {
        if (!options.Enabled || vehicle.LicensePlate is not { } plate) return null;

        DvlaVehicle? found;
        try
        {
            found = await client.GetVehicleAsync(plate, ct);
        }
        catch (DvlaUnavailableException ex)
        {
            // The plate stays out of the log: it is the one thing about the car that leaves the network.
            logger.LogWarning("DVLA lookup for vehicle {VehicleId} failed: {Reason}", vehicle.Id, ex.Message);
            await repository.SetErrorAsync(vehicle.Id, plate, ex.Message, ct);
            return await repository.GetAsync(vehicle.Id, ct);
        }

        var now = time.GetUtcNow().UtcDateTime;
        await repository.SaveAsync(found is null
            ? new DvlaVehicleInfo { VehicleId = vehicle.Id, LicensePlate = plate, Found = false, FetchedAt = now }
            : new DvlaVehicleInfo
            {
                VehicleId = vehicle.Id,
                LicensePlate = plate,
                Found = true,
                TaxStatus = found.TaxStatus,
                TaxDueDate = found.TaxDueDate,
                MotStatus = found.MotStatus,
                MotExpiryDate = found.MotExpiryDate,
                Make = found.Make,
                Colour = found.Colour,
                FuelType = found.FuelType,
                RecordJson = found.RecordJson,
                FetchedAt = now,
            }, ct);
        logger.LogInformation("DVLA lookup for vehicle {VehicleId}: plate known {Found}", vehicle.Id, found is not null);
        return await repository.GetAsync(vehicle.Id, ct);
    }
}
