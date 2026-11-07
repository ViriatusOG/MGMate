using GarageStack.Core.Helpers;
using GarageStack.Core.Interfaces;
using GarageStack.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GarageStack.Data.Repositories;

public class ChargeRepository(AppDbContext db, ITelemetryRepository telemetry) : IChargeRepository
{
    // More points than a chart's width has pixels for only slows it down.
    internal const int MaxChartPoints = 300;

    public Task<DateTime?> GetRecordedUntilAsync(int vehicleId, CancellationToken ct = default) =>
        db.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == vehicleId)
            .Select(v => v.ChargesRecordedUntil)
            .FirstOrDefaultAsync(ct);

    public async Task SaveRecordedAsync(int vehicleId, IReadOnlyList<ChargeSession> sessions, DateTime recordedUntil, CancellationToken ct = default)
    {
        var vehicle = await db.Vehicles.FindAsync([vehicleId], ct);
        if (vehicle is null) return;

        foreach (var session in sessions)
        {
            session.Id = 0;
            session.VehicleId = vehicleId;
            session.StopReason = session.StopReason.Truncate(64);
            db.ChargeSessions.Add(session);
        }

        vehicle.ChargesRecordedUntil = recordedUntil;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ChargeSessionDto>> GetLogAsync(int vehicleId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var rows = await db.ChargeSessions
            .AsNoTracking()
            .Where(c => c.VehicleId == vehicleId && c.StartedAt >= from && c.StartedAt < to)
            .OrderBy(c => c.StartedAt)
            .ToListAsync(ct);
        return [.. rows.Select(ChargeSessionDto.From)];
    }

    public async Task<ChargeSessionDto?> GetAsync(int vehicleId, long id, CancellationToken ct = default) =>
        await FindAsync(vehicleId, id, tracked: false, ct) is { } c ? ChargeSessionDto.From(c) : null;

    public async Task<IReadOnlyList<ChargeChartPoint>> GetChartAsync(int vehicleId, long id, CancellationToken ct = default)
    {
        if (await FindAsync(vehicleId, id, tracked: false, ct) is not { } session) return [];

        // A little either side, so the chart shows the level it started from and where it ended.
        var samples = await telemetry.GetChargeSamplesAsync(
            vehicleId, session.StartedAt.AddMinutes(-5), session.EndedAt + ChargeSegmenter.PauseThreshold, ct);
        var points = samples
            .Where(s => s.PowerKw is not null || s.SocPercent is not null)
            .Select(s => new ChargeChartPoint(s.RecordedAt, s.PowerKw is { } p ? Math.Abs(p) : null, s.SocPercent))
            .ToList();
        if (points.Count <= MaxChartPoints) return points;

        var step = (double)points.Count / MaxChartPoints;
        return [.. Enumerable.Range(0, MaxChartPoints).Select(i => points[(int)(i * step)])];
    }

    public async Task<ChargeSessionDto?> UpdateAsync(int vehicleId, long id, ChargeEdit edit, CancellationToken ct = default)
    {
        if (await FindAsync(vehicleId, id, tracked: true, ct) is not { } session) return null;
        session.Notes = edit.Notes;
        session.Place = edit.Place;
        session.PaidAmount = edit.PaidAmount;
        await db.SaveChangesAsync(ct);
        return ChargeSessionDto.From(session);
    }

    public async Task<ChargeSessionDto?> SetHiddenAsync(int vehicleId, long id, bool hidden, CancellationToken ct = default)
    {
        if (await FindAsync(vehicleId, id, tracked: true, ct) is not { } session) return null;
        session.IsHidden = hidden;
        await db.SaveChangesAsync(ct);
        return ChargeSessionDto.From(session);
    }

    public async Task<ChargeMergeResult> MergeWithNextAsync(int vehicleId, long id, CancellationToken ct = default)
    {
        if (await FindAsync(vehicleId, id, tracked: true, ct) is not { } first) return new(ChargeMergeOutcome.NotFound);

        var second = await db.ChargeSessions
            .Where(c => c.VehicleId == vehicleId && c.StartedAt > first.StartedAt)
            .OrderBy(c => c.StartedAt)
            .FirstOrDefaultAsync(ct);
        if (second is null) return new(ChargeMergeOutcome.NoNextCharge);

        first.EndedAt = second.EndedAt;
        first.SocEndPercent = second.SocEndPercent ?? first.SocEndPercent;
        first.EnergyAddedKwh = first.EnergyAddedKwh is null && second.EnergyAddedKwh is null
            ? null
            : (first.EnergyAddedKwh ?? 0) + (second.EnergyAddedKwh ?? 0);
        first.MaxPowerKw = Max(first.MaxPowerKw, second.MaxPowerKw);
        first.ChargeType = first.ChargeType == "DC" || second.ChargeType == "DC" ? "DC" : first.ChargeType ?? second.ChargeType;
        first.OdometerKm = second.OdometerKm ?? first.OdometerKm;
        first.StopReason = second.StopReason ?? first.StopReason;
        first.SocTargetPercent = second.SocTargetPercent ?? first.SocTargetPercent;
        first.Notes = (first.Notes, second.Notes) switch
        {
            (null, var n) => n,
            (var n, null) => n,
            var (a, b) => $"{a} / {b}".Truncate(ChargeSession.NotesMaxLength),
        };
        first.IsHidden = first.IsHidden && second.IsHidden;
        db.ChargeSessions.Remove(second);
        await db.SaveChangesAsync(ct);
        return new(ChargeMergeOutcome.Merged, ChargeSessionDto.From(first));
    }

    private static double? Max(double? a, double? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);

    private Task<ChargeSession?> FindAsync(int vehicleId, long id, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.ChargeSessions : db.ChargeSessions.AsNoTracking();
        return query.FirstOrDefaultAsync(c => c.VehicleId == vehicleId && c.Id == id, ct);
    }
}
