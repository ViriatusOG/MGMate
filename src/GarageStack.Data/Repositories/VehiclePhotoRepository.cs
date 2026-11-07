using GarageStack.Core.Interfaces;
using GarageStack.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GarageStack.Data.Repositories;

public class VehiclePhotoRepository(AppDbContext db) : IVehiclePhotoRepository
{
    public Task<VehiclePhoto?> GetAsync(int vehicleId, CancellationToken ct = default) =>
        db.VehiclePhotos.AsNoTracking().FirstOrDefaultAsync(p => p.VehicleId == vehicleId, ct);

    public Task<DateTime?> GetUpdatedAtAsync(int vehicleId, CancellationToken ct = default) =>
        db.VehiclePhotos.AsNoTracking()
            .Where(p => p.VehicleId == vehicleId)
            .Select(p => (DateTime?)p.UpdatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<DateTime> SaveAsync(int vehicleId, string contentType, byte[] data, CancellationToken ct = default)
    {
        var row = await db.VehiclePhotos.FirstOrDefaultAsync(p => p.VehicleId == vehicleId, ct);
        if (row is null)
        {
            row = new VehiclePhoto { VehicleId = vehicleId };
            db.VehiclePhotos.Add(row);
        }
        row.ContentType = contentType;
        row.Data = data;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return row.UpdatedAt;
    }

    public async Task<bool> DeleteAsync(int vehicleId, CancellationToken ct = default)
    {
        var row = await db.VehiclePhotos.FirstOrDefaultAsync(p => p.VehicleId == vehicleId, ct);
        if (row is null) return false;
        db.VehiclePhotos.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
