using MGMate.Core.Models;

namespace MGMate.Core.Interfaces;

/// <summary>The owner's photo of each vehicle (<see cref="VehiclePhoto"/>).</summary>
public interface IVehiclePhotoRepository
{
    Task<VehiclePhoto?> GetAsync(int vehicleId, CancellationToken ct = default);

    /// <summary>When the photo last changed, without loading it; null when there is none.</summary>
    Task<DateTime?> GetUpdatedAtAsync(int vehicleId, CancellationToken ct = default);

    /// <summary>Creates or replaces the vehicle's photo.</summary>
    Task<DateTime> SaveAsync(int vehicleId, string contentType, byte[] data, CancellationToken ct = default);

    /// <summary>Removes the photo; false when there was none.</summary>
    Task<bool> DeleteAsync(int vehicleId, CancellationToken ct = default);
}
