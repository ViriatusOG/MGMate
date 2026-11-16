using System.Text.Json;
using MGMate.Core.Configuration;
using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;
using MGMate.Core.Models;
using MGMate.Data.Services;

namespace MGMate.Api.Endpoints;

/// <summary>
/// What the DVLA says about the car's UK registration: road tax and MOT, and the rest of its
/// record. Answers 404 with <see cref="DisabledCode"/> while the deployment has no key of its own
/// (DVLA_API_KEY), which is also how the browser learns to leave the card out. The plate itself is
/// set through PUT /license-plate.
/// </summary>
public static class DvlaEndpoints
{
    public const string DisabledCode = "dvla.disabled";

    public static IEndpointRouteBuilder MapDvlaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vehicles/{vin}")
            .WithTags("DVLA")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveVehicleFilter>();

        // Deployment configuration, not request input.
        var options = app.ServiceProvider.GetRequiredService<DvlaOptions>();
        var time = app.ServiceProvider.GetRequiredService<TimeProvider>();

        // A GET, so read-only API tokens can read it too.
        group.MapGet("/dvla", async (
            HttpContext httpContext, IDvlaRepository dvla, DvlaRefresher refresher, CancellationToken ct) =>
        {
            if (!options.Enabled) return Disabled();

            var vehicle = httpContext.ResolvedVehicle();
            var info = await dvla.GetAsync(vehicle.Id, ct);
            // A plate that was saved before the key was set has never been asked about: ask now,
            // rather than leave the card empty until the Worker's next round. Once only: the
            // lookup leaves a row behind even when it fails, and the Worker retries those.
            if (NeverAsked(vehicle, info)) info = await refresher.RefreshAsync(vehicle, ct);
            return Results.Ok(ToDto(vehicle, info, time.GetUtcNow().UtcDateTime));
        })
        .WithSummary("The vehicle's plate and what the DVLA last said about it");

        // Asks the DVLA again now, for the card's "Check now".
        group.MapPost("/dvla/refresh", async (HttpContext httpContext, DvlaRefresher refresher, CancellationToken ct) =>
        {
            if (!options.Enabled) return Disabled();

            var vehicle = httpContext.ResolvedVehicle();
            var info = await refresher.RefreshAsync(vehicle, ct);
            return Results.Ok(ToDto(vehicle, info, time.GetUtcNow().UtcDateTime));
        })
        .WithSummary("Look the vehicle's plate up at the DVLA right away");

        return app;
    }

    internal static bool NeverAsked(Vehicle vehicle, DvlaVehicleInfo? info) =>
        vehicle.LicensePlate is { } plate && (info is null || info.LicensePlate != plate);

    private static IResult Disabled() =>
        ApiProblems.Problem(StatusCodes.Status404NotFound, DisabledCode,
            "DVLA lookups need this deployment's own key (DVLA_API_KEY)");

    internal static DvlaStatusDto ToDto(Vehicle vehicle, DvlaVehicleInfo? info, DateTime utcNow)
    {
        var plate = vehicle.LicensePlate;
        // A new car needs no MOT for three years; the date comes from the registration date the
        // user entered for the warranty.
        DateOnly? firstMotDue = vehicle.RegistrationDate is { } registered
            ? DvlaReminder.FirstMotDue(DateOnly.FromDateTime(registered))
            : null;

        // A lookup for another plate is about another car: the plate changed while it ran.
        if (plate is null || info is null || info.LicensePlate != plate)
            return new DvlaStatusDto(plate, firstMotDue, null);

        return new DvlaStatusDto(plate, firstMotDue, new DvlaInfoDto(
            info.Found,
            info.TaxStatus,
            info.TaxDueDate,
            // Counted on the UK calendar, as the notifications count it.
            info.TaxDueDate is { } taxDue ? DvlaReminder.DaysLeft(taxDue, utcNow) : null,
            info.MotStatus,
            info.MotExpiryDate,
            info.MotExpiryDate is { } motExpiry ? DvlaReminder.DaysLeft(motExpiry, utcNow) : null,
            info.Make,
            info.Colour,
            info.FuelType,
            Record(info.RecordJson),
            info.FetchedAt,
            info.LastError));
    }

    // Everything the DVLA sent, as it sent it.
    private static JsonElement Record(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var empty = JsonDocument.Parse("{}");
            return empty.RootElement.Clone();
        }
    }
}

/// <summary>
/// The plate (null when none is set), when a first MOT falls due going by the registration date
/// (null when that is not set), and the lookup for the plate (null until there is one).
/// </summary>
public record DvlaStatusDto(string? LicensePlate, DateOnly? FirstMotDue, DvlaInfoDto? Info);

/// <summary>
/// What the DVLA said. <see cref="Found"/> false means it knows no vehicle by the plate.
/// <see cref="Record"/> is its whole answer. <see cref="FetchedAt"/> is the last answer,
/// <see cref="LastError"/> why the attempt after it failed.
/// </summary>
public record DvlaInfoDto(
    bool Found,
    string? TaxStatus,
    DateOnly? TaxDueDate,
    int? TaxDaysLeft,
    string? MotStatus,
    DateOnly? MotExpiryDate,
    int? MotDaysLeft,
    string? Make,
    string? Colour,
    string? FuelType,
    JsonElement Record,
    DateTime? FetchedAt,
    string? LastError);
