using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;

namespace MGMate.Api.Endpoints;

public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/vehicles/{vin}/activity", async (
            HttpContext httpContext,
            ITelemetryRepository telemetry,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? gapMinutes,
            CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            // A week at most: every reading in the period is read.
            var rangeError = DateRange.TryResolve(from, to, TimeSpan.FromDays(1), TimeSpan.FromDays(7), out var start, out var end);
            if (rangeError is not null) return rangeError;
            if (gapMinutes is < 5 or > 24 * 60)
                return ApiProblems.BadRequest("activity.gapOutOfRange", "gapMinutes must be between 5 and 1440");

            var gap = gapMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : ActivityTimelineBuilder.DefaultGap;
            var samples = await telemetry.GetActivitySamplesAsync(vehicle.Id, start, end, ct);
            // The level the car last reported before the period, so the line starts at its left edge.
            var before = await telemetry.GetSocAtAsync(vehicle.Id, start.AddTicks(-1), ct);
            return Results.Ok(ActivityTimelineBuilder.Build(samples, start, end, gap, before?.Value));
        })
        .WithTags("Activity")
        .RequireAuthorization()
        .AddEndpointFilter<ResolveVehicleFilter>()
        .WithSummary("Get what the car did in a period (locks, doors, plug, charging, climate, drives), when it went quiet, and its readings per hour");

        return app;
    }
}
