using MGMate.Core.Models;

namespace MGMate.Core.Interfaces;

/// <summary>The DVLA lookup kept per vehicle (<see cref="DvlaVehicleInfo"/>).</summary>
public interface IDvlaRepository
{
    Task<DvlaVehicleInfo?> GetAsync(int vehicleId, CancellationToken ct = default);

    /// <summary>Every vehicle's row, for the Worker's round.</summary>
    Task<IReadOnlyList<DvlaVehicleInfo>> ListAsync(CancellationToken ct = default);

    /// <summary>Creates or replaces the vehicle's row with <paramref name="info"/>'s values.</summary>
    Task SaveAsync(DvlaVehicleInfo info, CancellationToken ct = default);

    /// <summary>Records that a lookup failed, leaving what the last good one found in place.</summary>
    Task SetErrorAsync(int vehicleId, string licensePlate, string error, CancellationToken ct = default);

    /// <summary>Forgets the vehicle's lookup, when its plate is cleared or changed.</summary>
    Task DeleteAsync(int vehicleId, CancellationToken ct = default);
}
