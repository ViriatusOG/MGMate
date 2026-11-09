using MGMate.Core.Models;

namespace MGMate.Core.Interfaces;

/// <summary>A vehicle's warranty covers.</summary>
public interface IWarrantyRepository
{
    /// <summary>The vehicle's covers, in their saved order.</summary>
    Task<IReadOnlyList<WarrantyCover>> ListAsync(int vehicleId, CancellationToken ct = default);

    Task<int> CountAsync(int vehicleId, CancellationToken ct = default);

    Task<WarrantyCover> AddAsync(WarrantyCover cover, CancellationToken ct = default);

    /// <summary>Applies <paramref name="change"/> and saves. Null when the vehicle has no such cover.</summary>
    Task<WarrantyCover?> UpdateAsync(int vehicleId, int id, Action<WarrantyCover> change, CancellationToken ct = default);

    /// <returns>false when the vehicle has no such cover.</returns>
    Task<bool> DeleteAsync(int vehicleId, int id, CancellationToken ct = default);

    /// <summary>Replaces every cover the vehicle has with <paramref name="covers"/>.</summary>
    Task ReplaceAllAsync(int vehicleId, IReadOnlyList<WarrantyCover> covers, CancellationToken ct = default);
}
