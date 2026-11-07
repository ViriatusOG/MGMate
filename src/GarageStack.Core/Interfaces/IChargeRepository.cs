using GarageStack.Core.Models;

namespace GarageStack.Core.Interfaces;

/// <summary>A vehicle's saved charges. <see cref="Vehicle.ChargesRecordedUntil"/> is the recording line.</summary>
public interface IChargeRepository
{
    /// <summary>The vehicle's <see cref="Vehicle.ChargesRecordedUntil"/>, or null when nothing has been recorded.</summary>
    Task<DateTime?> GetRecordedUntilAsync(int vehicleId, CancellationToken ct = default);

    /// <summary>Saves the sessions and moves the recording line in one transaction.</summary>
    Task SaveRecordedAsync(int vehicleId, IReadOnlyList<ChargeSession> sessions, DateTime recordedUntil, CancellationToken ct = default);

    /// <summary>Charges starting from <paramref name="from"/> up to (not including) <paramref name="to"/>, oldest first, hidden ones too.</summary>
    Task<IReadOnlyList<ChargeSessionDto>> GetLogAsync(int vehicleId, DateTime from, DateTime to, CancellationToken ct = default);

    Task<ChargeSessionDto?> GetAsync(int vehicleId, long id, CancellationToken ct = default);

    /// <summary>The power and charge level over the session, for its chart; empty when there is no such charge.</summary>
    Task<IReadOnlyList<ChargeChartPoint>> GetChartAsync(int vehicleId, long id, CancellationToken ct = default);

    Task<ChargeSessionDto?> SetNotesAsync(int vehicleId, long id, string? notes, CancellationToken ct = default);

    Task<ChargeSessionDto?> SetHiddenAsync(int vehicleId, long id, bool hidden, CancellationToken ct = default);

    /// <summary>Joins a charge with the next saved one: a plug-in the car reported as two.</summary>
    Task<ChargeMergeResult> MergeWithNextAsync(int vehicleId, long id, CancellationToken ct = default);
}
