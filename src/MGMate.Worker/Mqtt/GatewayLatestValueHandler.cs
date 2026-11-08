using System.Collections.Concurrent;
using MGMate.Core.Helpers;

namespace MGMate.Worker.Mqtt;

/// <summary>
/// Keeps the newest value of the gateway topics that have no use as a history (its polling phase
/// and last error, the car's make, model, year, series and colour) on the vehicle
/// record, so nothing the gateway reports is thrown away. See <see cref="GatewayDataCoverage"/>.
/// </summary>
public sealed class GatewayLatestValueHandler(ILogger logger, VehicleResolver vehicles) : IMqttMessageHandler
{
    // A long error message is cut; the full text stays in the gateway's own log.
    internal const int MaxValueLength = 1000;

    // Per VIN and subtopic, the value last stored: the gateway republishes these on every poll and
    // restart, and only a change is written.
    private readonly ConcurrentDictionary<(string Vin, string Subtopic), string> _stored = new();

    public async Task<bool> TryHandleAsync(MqttMessage message, CancellationToken ct)
    {
        if (message.Vehicle is not { } topic || !GatewayDataCoverage.LatestValueSubtopics.Contains(topic.Subtopic))
            return false;

        var value = message.Payload.Length > MaxValueLength ? message.Payload[..MaxValueLength] : message.Payload;
        var key = (topic.Vin, topic.Subtopic);
        if (_stored.TryGetValue(key, out var stored) && stored == value) return true;

        try
        {
            using var resolved = await vehicles.ResolveAsync(topic.Vin, topic.User, ct);
            await resolved.Vehicles.SetConfigValueAsync(
                resolved.VehicleId, GatewayDataCoverage.LatestValueKeyPrefix + topic.Subtopic, value, ct);
            // Only once written, so a failed write is tried again on the next message.
            _stored[key] = value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to store {Subtopic} for VIN={Vin}", topic.Subtopic, LogRedaction.Vin(topic.Vin));
        }

        return true;
    }
}
