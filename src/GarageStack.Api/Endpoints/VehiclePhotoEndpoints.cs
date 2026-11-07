using GarageStack.Core.Interfaces;
using GarageStack.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarageStack.Api.Endpoints;

/// <summary>
/// The owner's photo of the car, which the dashboard's overview card shows in place of the drawn
/// top view. The image travels as the raw request body (PNG, JPEG or WebP), and is served back
/// as-is with the type its bytes say it is.
/// </summary>
public static class VehiclePhotoEndpoints
{
    public static IEndpointRouteBuilder MapVehiclePhotoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vehicles/{vin}/photo")
            .WithTags("Vehicles")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveVehicleFilter>();

        group.MapGet("/", async (HttpContext httpContext, IVehiclePhotoRepository photos, CancellationToken ct) =>
        {
            var photo = await photos.GetAsync(httpContext.ResolvedVehicle().Id, ct);
            if (photo is null) return Results.NotFound();

            // The URL carries the upload time, so a cached copy can be kept until the photo changes.
            httpContext.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            httpContext.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(photo.Data, photo.ContentType, lastModified: new DateTimeOffset(photo.UpdatedAt, TimeSpan.Zero));
        })
        .WithSummary("The owner's photo of the car");

        group.MapGet("/info", async (HttpContext httpContext, IVehiclePhotoRepository photos, CancellationToken ct) =>
        {
            var updatedAt = await photos.GetUpdatedAtAsync(httpContext.ResolvedVehicle().Id, ct);
            return Results.Ok(new VehiclePhotoInfo(updatedAt is not null, updatedAt));
        })
        .WithSummary("Whether the car has a photo, and when it last changed");

        group.MapPut("/", async (HttpContext httpContext, IVehiclePhotoRepository photos, CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            var data = await ReadBodyAsync(httpContext.Request, ct);
            if (data is null)
                return ApiProblems.Problem(StatusCodes.Status413PayloadTooLarge, "vehiclePhoto.tooLarge",
                    $"A photo may be at most {VehiclePhoto.MaxBytes / (1024 * 1024)} MB");

            var contentType = VehiclePhoto.DetectContentType(data);
            if (contentType is null)
                return ApiProblems.BadRequest("vehiclePhoto.unsupported", "The photo must be a PNG, JPEG or WebP image");

            var updatedAt = await photos.SaveAsync(vehicle.Id, contentType, data, ct);
            return Results.Ok(new VehiclePhotoInfo(true, updatedAt));
        })
        .WithMetadata(new RequestSizeLimitAttribute(VehiclePhoto.MaxBytes + 1))
        .WithSummary("Upload a photo of the car (the image file as the body), replacing any earlier one");

        group.MapDelete("/", async (HttpContext httpContext, IVehiclePhotoRepository photos, CancellationToken ct) =>
        {
            await photos.DeleteAsync(httpContext.ResolvedVehicle().Id, ct);
            return Results.NoContent();
        })
        .WithSummary("Remove the car's photo, so the card draws the car again");

        return app;
    }

    // The body, or null when it runs past the limit. Read in full here so a client that leaves
    // out Content-Length still cannot send more than the limit.
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength > VehiclePhoto.MaxBytes) return null;
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > VehiclePhoto.MaxBytes) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }
}

public record VehiclePhotoInfo(bool HasPhoto, DateTime? UpdatedAt);
