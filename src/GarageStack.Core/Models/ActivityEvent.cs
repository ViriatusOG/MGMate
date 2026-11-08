namespace GarageStack.Core.Models;

/// <summary>What the activity timeline needs of one telemetry reading.</summary>
public sealed record ActivitySample(
    DateTime RecordedAt,
    bool? IsLocked,
    bool? AnyDoorOpen,
    bool? ChargerConnected,
    bool? IsCharging,
    bool? ClimateOn,
    bool? EngineRunning,
    double? SocPercent);

/// <summary>
/// Something the car did, read off two readings in a row that disagree: "locked", "unlocked",
/// "doorOpened", "doorsClosed", "plugged", "unplugged", "chargingStarted", "chargingStopped",
/// "climateOn", "climateOff", "driveStarted" or "driveEnded".
/// </summary>
public sealed record ActivityEvent(DateTime At, string Kind, double? SocPercent);

/// <summary>A stretch with no reading from the car for longer than the timeline's threshold.</summary>
public sealed record ActivityGap(DateTime From, DateTime To);

/// <summary>How many readings arrived in one hour: the link to the car, as the database saw it.</summary>
public sealed record ActivityHour(DateTime Hour, int Readings);

public sealed record ActivityTimeline(
    IReadOnlyList<ActivityEvent> Events,
    IReadOnlyList<ActivityGap> Gaps,
    IReadOnlyList<ActivityHour> Hours,
    int Readings);
