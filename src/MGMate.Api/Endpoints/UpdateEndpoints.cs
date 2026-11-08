using MGMate.Api.Services;

namespace MGMate.Api.Endpoints;

/// <summary>Whether a newer MGMate release is out, for the footer.</summary>
public static class UpdateEndpoints
{
    public static IEndpointRouteBuilder MapUpdateEndpoints(this IEndpointRouteBuilder app)
    {
        var updates = app.ServiceProvider.GetRequiredService<UpdateCheckService>();

        // Signed in only: an anonymous visitor has no use for the version this install runs.
        app.MapGet("/api/updates", () => Results.Ok(updates.Latest))
            .WithTags("Updates")
            .WithSummary("Whether a newer MGMate release is available")
            .RequireAuthorization();

        return app;
    }
}
