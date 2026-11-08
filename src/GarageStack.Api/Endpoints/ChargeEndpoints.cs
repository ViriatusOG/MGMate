using GarageStack.Core.Interfaces;
using GarageStack.Core.Models;

namespace GarageStack.Api.Endpoints;

public static class ChargeEndpoints
{
    public static IEndpointRouteBuilder MapChargeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vehicles/{vin}/charges")
            .WithTags("Charges")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveVehicleFilter>();

        group.MapGet("/", async (
            HttpContext httpContext,
            IChargeRepository charges,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            var rangeError = DateRange.TryResolve(from, to, TimeSpan.FromDays(31), TimeSpan.FromDays(366), out var start, out var end);
            if (rangeError is not null) return rangeError;

            return Results.Ok(await charges.GetLogAsync(vehicle.Id, start, end, ct));
        })
        .WithSummary("Get the saved charges in a period, hidden ones included");

        group.MapGet("/{id:long}/chart", async (HttpContext httpContext, long id, IChargeRepository charges, CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            if (await charges.GetAsync(vehicle.Id, id, ct) is null) return Results.NotFound();
            return Results.Ok(await charges.GetChartAsync(vehicle.Id, id, ct));
        })
        .WithSummary("Get the battery power and charge level over a saved charge");

        group.MapPut("/{id:long}", async (
            HttpContext httpContext,
            long id,
            ChargeUpdateRequest req,
            IChargeRepository charges,
            CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            var notes = TripEndpoints.NormalizeNotes(req.Notes);
            if (notes is { Length: > ChargeSession.NotesMaxLength })
                return ApiProblems.BadRequest("charge.notesTooLong", $"Notes must be {ChargeSession.NotesMaxLength} characters or fewer");

            if (req.Place is not null && !ChargePlaces.All.Contains(req.Place))
                return ApiProblems.BadRequest("charge.placeUnknown", "place must be home, work, public or null");
            if (req.PaidAmount is < 0 or > ChargeSession.PaidAmountMax)
                return ApiProblems.BadRequest("charge.paidOutOfRange", $"paidAmount must be between 0 and {ChargeSession.PaidAmountMax}");

            var paid = req.PaidAmount is { } amount ? Math.Round(amount, 2) : (decimal?)null;
            var updated = await charges.UpdateAsync(vehicle.Id, id, new ChargeEdit(notes, req.Place, paid), ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithSummary("Set a charge's notes, place and the price paid, replacing what was there");

        group.MapPut("/{id:long}/hidden", async (
            HttpContext httpContext,
            long id,
            ChargeHiddenRequest req,
            IChargeRepository charges,
            CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            if (req.Hidden is not { } hidden)
                return ApiProblems.BadRequest("charge.hiddenRequired", "hidden must be true or false");

            var updated = await charges.SetHiddenAsync(vehicle.Id, id, hidden, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithSummary("Hide a charge from the totals, or show it again");

        group.MapPost("/{id:long}/merge-next", async (HttpContext httpContext, long id, IChargeRepository charges, CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            var result = await charges.MergeWithNextAsync(vehicle.Id, id, ct);
            return result.Outcome switch
            {
                ChargeMergeOutcome.Merged => Results.Ok(result.Merged),
                ChargeMergeOutcome.NoNextCharge => ApiProblems.Problem(StatusCodes.Status409Conflict, "charge.noNextCharge", "No saved charge follows this one"),
                _ => Results.NotFound(),
            };
        })
        .WithSummary("Join a charge with the next saved one, when the car reported one plug-in as two");

        return app;
    }
}

/// <summary>
/// A charge's editable fields, all sent each time. Place is "home", "work", "public" or null for
/// automatic; PaidAmount is what the charge cost in the owner's currency, or null to use the tariff.
/// </summary>
public record ChargeUpdateRequest(string? Notes, string? Place, decimal? PaidAmount);

public record ChargeHiddenRequest(bool? Hidden);
