namespace MGMate.Core.Models;

/// <summary>
/// When the gateway last signed in to the MG account, and when it last failed to. The Worker hears
/// both from the gateway's retained account topics and passes them to the API in its heartbeat, so
/// the web interface can say that MG data has stopped because the sign-in fails (a wrong password
/// or user name, or SAIC ending the session), not merely show the last state the car reported.
/// </summary>
public sealed record GatewaySignIn(DateTimeOffset? LastSignInAt, DateTimeOffset? LastFailureAt)
{
    /// <summary>Failing while the latest attempt the gateway reported is a failed one.</summary>
    public bool Failing => LastFailureAt is { } failedAt && (LastSignInAt is not { } signedInAt || failedAt > signedInAt);
}
