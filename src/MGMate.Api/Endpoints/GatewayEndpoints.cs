using MGMate.Api.Health;

namespace MGMate.Api.Endpoints;

/// <summary>Whether the gateway can sign in to the MG account, for the sidebar's status.</summary>
public static class GatewayEndpoints
{
    public static IEndpointRouteBuilder MapGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        // Demo mode has no Worker, so no heartbeat monitor: it never reports a failing sign-in.
        app.MapGet("/api/gateway/sign-in", (IServiceProvider services) =>
            {
                var signIn = services.GetService<WorkerHeartbeatMonitor>()?.GatewaySignIn;
                return Results.Ok(new GatewaySignInResponse(
                    signIn?.Failing ?? false, signIn?.LastSignInAt, signIn?.LastFailureAt));
            })
            .WithTags("Gateway")
            .WithSummary("Whether the gateway's sign-in to the MG account is failing")
            .RequireAuthorization();

        return app;
    }
}

public sealed record GatewaySignInResponse(bool Failing, DateTimeOffset? LastSignInAt, DateTimeOffset? LastFailureAt);
