using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;
using MGMate.Core.Models;

namespace MGMate.Api.Endpoints;

/// <summary>
/// The car's warranty: its registration and delivery dates, and the list of covers with how much
/// of each is left. Every change answers with the whole warranty, so the page redraws from one
/// response.
/// </summary>
public static class WarrantyEndpoints
{
    public static IEndpointRouteBuilder MapWarrantyEndpoints(this IEndpointRouteBuilder app)
    {
        var vehicleGroup = app.MapGroup("/api/vehicles/{vin}")
            .WithTags("Warranty")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveVehicleFilter>();

        vehicleGroup.MapGet("/warranty", async (
            HttpContext httpContext, IWarrantyRepository warranty, ITelemetryRepository telemetry, CancellationToken ct) =>
            Results.Ok(await LoadAsync(httpContext.ResolvedVehicle(), warranty, telemetry, ct)))
        .WithSummary("The car's warranty dates and covers, with what is left of each");

        vehicleGroup.MapPut("/ownership-dates", async (
            HttpContext httpContext, OwnershipDatesRequest req,
            IVehicleRepository vehicles, IWarrantyRepository warranty, ITelemetryRepository telemetry, CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            if (ValidateDates(req, DateTime.UtcNow) is { } error)
                return ApiProblems.BadRequest(error);

            var registration = AsUtcDay(req.RegistrationDate);
            var delivery = AsUtcDay(req.DeliveryDate);
            await vehicles.SetOwnershipDatesAsync(vehicle.Id, registration, delivery, req.DeliveryOdometerKm, ct);
            vehicle.RegistrationDate = registration;
            vehicle.DeliveryDate = delivery;
            vehicle.DeliveryOdometerKm = req.DeliveryOdometerKm;

            // The first time a start date is known, the list starts as MG's covers.
            if (registration is not null && await warranty.CountAsync(vehicle.Id, ct) == 0)
                await warranty.ReplaceAllAsync(vehicle.Id, WarrantyDefaults.For(vehicle.Id), ct);

            return Results.Ok(await LoadAsync(vehicle, warranty, telemetry, ct));
        })
        .WithSummary("Set the car's first registration date, delivery date and delivery odometer reading");

        var group = vehicleGroup.MapGroup("/warranty");

        group.MapPost("/covers", async (
            HttpContext httpContext, WarrantyCoverRequest req,
            IWarrantyRepository warranty, ITelemetryRepository telemetry, CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            if (ValidateCover(req) is { } error)
                return ApiProblems.BadRequest(error);
            var count = await warranty.CountAsync(vehicle.Id, ct);
            if (count >= WarrantyLimits.MaxCovers)
                return ApiProblems.BadRequest(new("warranty.tooManyCovers", $"A car can have at most {WarrantyLimits.MaxCovers} covers"));

            await warranty.AddAsync(Apply(new WarrantyCover { VehicleId = vehicle.Id, SortOrder = count }, req), ct);
            return Results.Ok(await LoadAsync(vehicle, warranty, telemetry, ct));
        })
        .WithSummary("Add a warranty cover");

        group.MapPut("/covers/{id:int}", async (
            HttpContext httpContext, int id, WarrantyCoverRequest req,
            IWarrantyRepository warranty, ITelemetryRepository telemetry, CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            if (ValidateCover(req) is { } error)
                return ApiProblems.BadRequest(error);
            if (await warranty.UpdateAsync(vehicle.Id, id, c => Apply(c, req), ct) is null)
                return Results.NotFound();
            return Results.Ok(await LoadAsync(vehicle, warranty, telemetry, ct));
        })
        .WithSummary("Change a warranty cover");

        group.MapDelete("/covers/{id:int}", async (
            HttpContext httpContext, int id,
            IWarrantyRepository warranty, ITelemetryRepository telemetry, CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            if (!await warranty.DeleteAsync(vehicle.Id, id, ct))
                return Results.NotFound();
            return Results.Ok(await LoadAsync(vehicle, warranty, telemetry, ct));
        })
        .WithSummary("Remove a warranty cover");

        group.MapPost("/reset", async (
            HttpContext httpContext, IWarrantyRepository warranty, ITelemetryRepository telemetry, CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            await warranty.ReplaceAllAsync(vehicle.Id, WarrantyDefaults.For(vehicle.Id), ct);
            return Results.Ok(await LoadAsync(vehicle, warranty, telemetry, ct));
        })
        .WithSummary("Replace the covers with MG's defaults");

        return app;
    }

    /// <summary>A calendar day as midnight UTC, which a timestamptz column accepts.</summary>
    internal static DateTime? AsUtcDay(DateOnly? day) =>
        day?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    internal static ValidationError? ValidateDates(OwnershipDatesRequest req, DateTime nowUtc)
    {
        var tomorrow = DateOnly.FromDateTime(nowUtc).AddDays(1);
        var earliest = new DateOnly(1990, 1, 1);
        if (req.RegistrationDate is { } r && (r > tomorrow || r < earliest))
            return new("warranty.registrationDateOutOfRange", "Registration date must be a past date");
        if (req.DeliveryDate is { } d && (d > tomorrow || d < earliest))
            return new("warranty.deliveryDateOutOfRange", "Delivery date must be a past date");
        if (req is { RegistrationDate: { } reg, DeliveryDate: { } del } && del < reg)
            return new("warranty.deliveryBeforeRegistration", "Delivery date cannot be before the registration date");
        if (req.DeliveryOdometerKm is < 0 or > WarrantyLimits.MaxDistanceKm)
            return new("warranty.odometerOutOfRange", "Delivery odometer reading is out of range");
        return null;
    }

    internal static ValidationError? ValidateCover(WarrantyCoverRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return new("warranty.nameRequired", "Name is required");
        if (req.Name.Trim().Length > WarrantyLimits.NameMaxLength)
            return new("warranty.nameTooLong", $"Name must be {WarrantyLimits.NameMaxLength} characters or fewer");
        if (req.Notes is { Length: > WarrantyLimits.NotesMaxLength })
            return new("warranty.notesTooLong", $"Notes must be {WarrantyLimits.NotesMaxLength} characters or fewer");
        if (req.Months is < 0 or > WarrantyLimits.MaxMonths)
            return new("warranty.monthsOutOfRange", $"Months must be between 0 and {WarrantyLimits.MaxMonths}");
        if (req.DistanceKm is not null and (<= 0 or > WarrantyLimits.MaxDistanceKm))
            return new("warranty.distanceOutOfRange", "Distance limit is out of range");
        return null;
    }

    private static WarrantyCover Apply(WarrantyCover cover, WarrantyCoverRequest req)
    {
        cover.Name = req.Name.Trim();
        cover.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        cover.Months = req.Months;
        cover.DistanceKm = req.Months == 0 ? null : req.DistanceKm;
        cover.Featured = req.Featured;
        return cover;
    }

    private static async Task<WarrantyDto> LoadAsync(
        Vehicle vehicle, IWarrantyRepository warranty, ITelemetryRepository telemetry, CancellationToken ct)
    {
        var covers = await warranty.ListAsync(vehicle.Id, ct);
        var odometerKm = await telemetry.GetOdometerAtAsync(vehicle.Id, DateTime.UtcNow, ct);
        return ToDto(vehicle, covers, odometerKm, DateTime.UtcNow);
    }

    internal static WarrantyDto ToDto(Vehicle vehicle, IReadOnlyList<WarrantyCover> covers, double? odometerKm, DateTime nowUtc) =>
        new(vehicle.RegistrationDate, vehicle.DeliveryDate, vehicle.DeliveryOdometerKm, odometerKm,
            covers.Select(c =>
            {
                var r = WarrantyCalculator.Calculate(c.Months, c.DistanceKm, vehicle.RegistrationDate, odometerKm, nowUtc);
                return new WarrantyCoverDto(
                    c.Id, c.Name, c.Notes, c.Months, c.DistanceKm, c.Featured, c.SortOrder,
                    r.Status.ApiName(), r.EndsOn, r.DaysRemaining, r.KmRemaining, r.TimeUsed, r.DistanceUsed, r.EndedBy);
            }).ToList());
}

public record OwnershipDatesRequest(DateOnly? RegistrationDate, DateOnly? DeliveryDate, double? DeliveryOdometerKm);

public record WarrantyCoverRequest(string Name, string? Notes, int Months, double? DistanceKm, bool Featured);

public record WarrantyDto(
    DateTime? RegistrationDate, DateTime? DeliveryDate, double? DeliveryOdometerKm, double? OdometerKm,
    IReadOnlyList<WarrantyCoverDto> Covers);

public record WarrantyCoverDto(
    int Id, string Name, string? Notes, int Months, double? DistanceKm, bool Featured, int SortOrder,
    string Status, DateTime? EndsOn, int? DaysRemaining, double? KmRemaining,
    double? TimeUsed, double? DistanceUsed, string? EndedBy);
