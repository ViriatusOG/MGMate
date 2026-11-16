using MGMate.Core.Models;

namespace MGMate.Core.Interfaces;

/// <summary>
/// The DVLA's Vehicle Enquiry Service, asked by plate. Throws <see cref="DvlaUnavailableException"/>
/// when the DVLA could not be asked (no key, rate-limited, unreachable, key refused) or answered
/// with something other than a vehicle.
/// </summary>
public interface IDvlaClient
{
    /// <returns>The vehicle, or null when the DVLA has none under <paramref name="plate"/>.</returns>
    Task<DvlaVehicle?> GetVehicleAsync(string plate, CancellationToken ct = default);
}
