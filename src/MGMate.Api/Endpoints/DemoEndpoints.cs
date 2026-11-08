using MGMate.Core.Interfaces;
using MGMate.Core.Models;
using MGMate.Data.Demo;

namespace MGMate.Api.Endpoints;

public static class DemoEndpoints
{
    public static IEndpointRouteBuilder MapDemoEndpoints(
        this IEndpointRouteBuilder app,
        ITelemetryRepository telemetry)
    {
        var demoRepo = telemetry as DemoTelemetryRepository
            ?? throw new InvalidOperationException(
                $"{nameof(MapDemoEndpoints)} requires {nameof(DemoTelemetryRepository)} to be registered as {nameof(ITelemetryRepository)} - check demo mode DI wiring in Program.cs.");

        var group = app.MapGroup("/api/demo")
            .WithTags("Demo")
            .RequireAuthorization();

        group.MapGroup("/status/{vin}")
            .AddEndpointFilter<ResolveVehicleFilter>()
            .MapPost("/", (DemoStatusOverrideDto dto) =>
            {
                demoRepo.ApplyOverride(dto);
                return Results.NoContent();
            })
            .WithSummary("Override demo vehicle status fields");

        return app;
    }
}
