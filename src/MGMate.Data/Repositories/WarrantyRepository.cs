using MGMate.Core.Interfaces;
using MGMate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace MGMate.Data.Repositories;

public class WarrantyRepository(AppDbContext db) : IWarrantyRepository
{
    public async Task<IReadOnlyList<WarrantyCover>> ListAsync(int vehicleId, CancellationToken ct = default) =>
        await db.WarrantyCovers.AsNoTracking()
            .Where(c => c.VehicleId == vehicleId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .ToListAsync(ct);

    public Task<int> CountAsync(int vehicleId, CancellationToken ct = default) =>
        db.WarrantyCovers.CountAsync(c => c.VehicleId == vehicleId, ct);

    public async Task<WarrantyCover> AddAsync(WarrantyCover cover, CancellationToken ct = default)
    {
        db.WarrantyCovers.Add(cover);
        await db.SaveChangesAsync(ct);
        return cover;
    }

    public async Task<WarrantyCover?> UpdateAsync(int vehicleId, int id, Action<WarrantyCover> change, CancellationToken ct = default)
    {
        var cover = await db.WarrantyCovers.FirstOrDefaultAsync(c => c.Id == id && c.VehicleId == vehicleId, ct);
        if (cover is null) return null;
        change(cover);
        await db.SaveChangesAsync(ct);
        return cover;
    }

    public async Task<bool> DeleteAsync(int vehicleId, int id, CancellationToken ct = default)
    {
        var cover = await db.WarrantyCovers.FirstOrDefaultAsync(c => c.Id == id && c.VehicleId == vehicleId, ct);
        if (cover is null) return false;
        db.WarrantyCovers.Remove(cover);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ReplaceAllAsync(int vehicleId, IReadOnlyList<WarrantyCover> covers, CancellationToken ct = default)
    {
        var old = await db.WarrantyCovers.Where(c => c.VehicleId == vehicleId).ToListAsync(ct);
        db.WarrantyCovers.RemoveRange(old);
        db.WarrantyCovers.AddRange(covers);
        await db.SaveChangesAsync(ct);
    }
}
