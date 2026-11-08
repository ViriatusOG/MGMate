namespace MGMate.Core.Models;

public record VehicleAggregateStats(
    int? ClimateUsagePct,
    int ClimateOnSnapshots,
    int TotalClimateSnapshots
);
