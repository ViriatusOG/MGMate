namespace GarageStack.Core.Models;

/// <summary>
/// A finished charge, saved by the Worker once no later reading can extend it: one row per plug-in,
/// built from the charging readings the car already reports, the way a trip is built from its fixes.
/// </summary>
public class ChargeSession
{
    public const int NotesMaxLength = 500;

    public long Id { get; set; }
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>The first reading that said the car was charging.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>The last reading that said the car was charging.</summary>
    public DateTime EndedAt { get; set; }

    public double? SocStartPercent { get; set; }
    public double? SocEndPercent { get; set; }

    /// <summary>
    /// The energy into the battery, from the battery power integrated over the session; null when
    /// the car reported too little power to integrate. Clients fall back on the state of charge.
    /// </summary>
    public double? EnergyAddedKwh { get; set; }

    /// <summary>The highest charging power reported, in kW.</summary>
    public double? MaxPowerKw { get; set; }

    /// <summary>"AC" or "DC": from the charging type the car reports, else from the power.</summary>
    public string? ChargeType { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? OdometerKm { get; set; }

    /// <summary>Why the car stopped, as the gateway names it, when it said.</summary>
    public string? StopReason { get; set; }

    /// <summary>The charge level the car was set to stop at, in percent.</summary>
    public int? SocTargetPercent { get; set; }

    public string? Notes { get; set; }

    /// <summary>Kept out of the totals; shown again on request, never deleted.</summary>
    public bool IsHidden { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A charge as the Api serves it.</summary>
public sealed record ChargeSessionDto(
    long Id,
    DateTime StartedAt,
    DateTime EndedAt,
    double? SocStartPercent,
    double? SocEndPercent,
    double? EnergyAddedKwh,
    double? MaxPowerKw,
    string? ChargeType,
    double? Latitude,
    double? Longitude,
    double? OdometerKm,
    string? StopReason,
    int? SocTargetPercent,
    string? Notes,
    bool IsHidden)
{
    public static ChargeSessionDto From(ChargeSession s) => new(
        s.Id, s.StartedAt, s.EndedAt, s.SocStartPercent, s.SocEndPercent, s.EnergyAddedKwh, s.MaxPowerKw,
        s.ChargeType, s.Latitude, s.Longitude, s.OdometerKm, s.StopReason, s.SocTargetPercent, s.Notes, s.IsHidden);
}

/// <summary>
/// One telemetry reading as far as charging goes. Power is the battery's, in kW, of either sign:
/// the gateway reports charging as negative current on some cars.
/// </summary>
public sealed record ChargeSample(
    DateTime RecordedAt,
    bool? IsCharging,
    double? SocPercent,
    double? PowerKw,
    string? ChargingType,
    double? Latitude,
    double? Longitude,
    double? OdometerKm,
    string? StopReason,
    int? SocTargetPercent);

/// <summary>One point of a charge's chart: when, the power in kW and the state of charge.</summary>
public sealed record ChargeChartPoint(DateTime RecordedAt, double? PowerKw, double? SocPercent);

/// <summary>How joining a charge with the next one went.</summary>
public enum ChargeMergeOutcome
{
    Merged,
    NotFound,
    NoNextCharge,
}

public sealed record ChargeMergeResult(ChargeMergeOutcome Outcome, ChargeSessionDto? Merged = null);
