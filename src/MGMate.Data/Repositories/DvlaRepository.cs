using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;
using MGMate.Core.Models;
using MGMate.Data.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MGMate.Data.Repositories;

public class DvlaRepository(AppDbContext db) : IDvlaRepository
{
    public Task<DvlaVehicleInfo?> GetAsync(int vehicleId, CancellationToken ct = default) =>
        db.DvlaVehicleInfos.AsNoTracking().FirstOrDefaultAsync(r => r.VehicleId == vehicleId, ct);

    public async Task<IReadOnlyList<DvlaVehicleInfo>> ListAsync(CancellationToken ct = default) =>
        await db.DvlaVehicleInfos.AsNoTracking().OrderBy(r => r.VehicleId).ToListAsync(ct);

    public async Task SaveAsync(DvlaVehicleInfo info, CancellationToken ct = default)
    {
        var row = await TrackedOrNewAsync(info.VehicleId, ct);
        row.LicensePlate = info.LicensePlate;
        row.Found = info.Found;
        row.TaxStatus = info.TaxStatus;
        row.TaxDueDate = info.TaxDueDate;
        row.MotStatus = info.MotStatus;
        row.MotExpiryDate = info.MotExpiryDate;
        row.Make = info.Make;
        row.Colour = info.Colour;
        row.FuelType = info.FuelType;
        row.RecordJson = info.RecordJson;
        row.FetchedAt = info.FetchedAt;
        row.LastError = info.LastError?.Truncate(DvlaLimits.ErrorMaxLength);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        // The API and the Worker can both create a vehicle's first row at the same moment; the
        // loser of that race writes over the winner's row instead.
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            db.ChangeTracker.Clear();
            await SaveAsync(info, ct);
        }
    }

    public async Task SetErrorAsync(int vehicleId, string licensePlate, string error, CancellationToken ct = default)
    {
        var row = await TrackedOrNewAsync(vehicleId, ct);
        // An error about another plate says nothing about the old plate's data: start afresh
        // rather than show the old car's tax under the new plate.
        if (row.LicensePlate != licensePlate)
        {
            row.LicensePlate = licensePlate;
            row.Found = false;
            row.TaxStatus = row.MotStatus = row.Make = row.Colour = row.FuelType = null;
            row.TaxDueDate = row.MotExpiryDate = null;
            row.RecordJson = "{}";
            row.FetchedAt = null;
        }
        row.LastError = error.Truncate(DvlaLimits.ErrorMaxLength);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int vehicleId, CancellationToken ct = default)
    {
        var row = await db.DvlaVehicleInfos.FirstOrDefaultAsync(r => r.VehicleId == vehicleId, ct);
        if (row is null) return;
        db.DvlaVehicleInfos.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    private async Task<DvlaVehicleInfo> TrackedOrNewAsync(int vehicleId, CancellationToken ct)
    {
        var row = await db.DvlaVehicleInfos.FirstOrDefaultAsync(r => r.VehicleId == vehicleId, ct);
        if (row is not null) return row;

        row = new DvlaVehicleInfo { VehicleId = vehicleId };
        db.DvlaVehicleInfos.Add(row);
        return row;
    }
}
