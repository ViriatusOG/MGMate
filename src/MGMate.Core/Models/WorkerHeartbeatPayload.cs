using System.Text.Json;

namespace MGMate.Core.Models;

/// <summary>
/// What the Worker reports about itself every few seconds on the worker_heartbeat channel, so the
/// API's health check can speak for the whole stack. The heartbeat arriving at all proves the
/// Worker runs and reaches the database; this adds whether it is subscribed to the broker, and how
/// the gateway's sign-in to the MG account last went.
/// </summary>
public sealed record WorkerHeartbeatPayload(bool MqttConnected, GatewaySignIn? GatewaySignIn = null)
{
    public string ToJson() => PgNotifyJson.Serialize(this);

    public static WorkerHeartbeatPayload? FromJson(string json)
    {
        try
        {
            return PgNotifyJson.Deserialize<WorkerHeartbeatPayload>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
