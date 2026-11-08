using MGMate.Core.Models;

namespace MGMate.Worker.Mqtt;

/// <summary>
/// The gateway's latest sign-in and sign-in failure, as <see cref="AccountStatusHandler"/> hears
/// them. Read by the Worker's heartbeat, which passes them to the API.
/// </summary>
public sealed class GatewaySignInState
{
    private readonly Lock _lock = new();
    private DateTimeOffset? _lastSignInAt;
    private DateTimeOffset? _lastFailureAt;

    public GatewaySignIn Current
    {
        get
        {
            lock (_lock) return new GatewaySignIn(_lastSignInAt, _lastFailureAt);
        }
    }

    internal void SignedIn(DateTimeOffset at)
    {
        lock (_lock) _lastSignInAt = Latest(_lastSignInAt, at);
    }

    internal void Failed(DateTimeOffset at)
    {
        lock (_lock) _lastFailureAt = Latest(_lastFailureAt, at);
    }

    private static DateTimeOffset Latest(DateTimeOffset? known, DateTimeOffset at) =>
        known is { } k && k > at ? k : at;
}
