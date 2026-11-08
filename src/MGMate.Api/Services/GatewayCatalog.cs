using System.Collections.Concurrent;
using System.Text.Json;
using MGMate.Core.Configuration;
using MGMate.Core.Helpers;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace MGMate.Api.Services;

/// <summary>One entity the gateway announces for a car, as its Home Assistant discovery config describes it.</summary>
public sealed record GatewayEntity(
    string Id,
    string Component,
    string Name,
    string? EntityCategory,
    bool EnabledByDefault,
    string? DeviceClass,
    string? Unit,
    string? Icon,
    string? StateSubtopic,
    string? ValueTemplate,
    IReadOnlyDictionary<string, string> CommandSubtopics,
    IReadOnlyDictionary<string, JsonElement> Attributes);

/// <summary>One value the gateway published: its payload, and when it arrived if that is known.</summary>
/// <param name="Payload">The value as the gateway published it.</param>
/// <param name="ReceivedAt">Null for a retained value the broker handed over on subscribing: it was published at some point before then.</param>
public sealed record GatewayValue(string Payload, DateTimeOffset? ReceivedAt);

/// <summary>The car itself, from the device block of its discovery configs.</summary>
public sealed record GatewayDevice(string? Name, string? Manufacturer, string? Model, string? HardwareVersion, string? GatewayVersion);

/// <summary>Everything the gateway has said about one car.</summary>
public sealed record GatewayVehicleCatalog(
    GatewayDevice? Device,
    IReadOnlyList<GatewayEntity> Entities,
    IReadOnlyDictionary<string, GatewayValue> Values,
    DateTimeOffset ListeningSince);

/// <summary>What the gateway announces and publishes per car, for the Debug page.</summary>
public interface IGatewayCatalog
{
    /// <summary>Whether the catalog is connected to the broker and receiving.</summary>
    bool Connected { get; }

    GatewayVehicleCatalog Get(string vin);
}

/// <summary>
/// Listens to the broker for the gateway's discovery configs and every vehicle value, both of
/// which the gateway publishes retained, so a fresh subscription is handed the full picture at
/// once. Kept in memory only: the Worker stores what is worth storing, this is for looking.
/// </summary>
public sealed class GatewayCatalog(IOptions<MqttOptions> options, ILogger<GatewayCatalog> logger, TimeProvider? timeProvider = null)
    : BackgroundService, IGatewayCatalog
{
    private readonly MqttOptions _options = options.Value;
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, VehicleState> _vehicles = new(StringComparer.Ordinal);
    private DateTimeOffset _listeningSince;
    private volatile bool _connected;

    private sealed class VehicleState
    {
        public readonly ConcurrentDictionary<string, GatewayEntity> Entities = new(StringComparer.Ordinal);
        public readonly ConcurrentDictionary<string, GatewayValue> Values = new(StringComparer.Ordinal);
        public GatewayDevice? Device;
    }

    public bool Connected => _connected;

    public GatewayVehicleCatalog Get(string vin)
    {
        if (!_vehicles.TryGetValue(vin, out var state))
            return new GatewayVehicleCatalog(null, [], new Dictionary<string, GatewayValue>(), _listeningSince);
        return new GatewayVehicleCatalog(
            state.Device,
            [.. state.Entities.Values.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)],
            new Dictionary<string, GatewayValue>(state.Values, StringComparer.Ordinal),
            _listeningSince);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var client = new MqttClientFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e =>
        {
            Receive(e.ApplicationMessage.Topic, e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty, e.ApplicationMessage.Retain);
            return Task.CompletedTask;
        };

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId("mgmate-api-catalog")
            .WithCleanSession();
        if (!string.IsNullOrWhiteSpace(_options.Username))
            builder.WithCredentials(_options.Username, _options.Password);
        var clientOptions = builder.Build();
        var outage = new OutageLog(logger, "Debug page broker connection", TimeSpan.FromMinutes(15));

        while (!stoppingToken.IsCancellationRequested)
        {
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<MqttClientDisconnectedEventArgs, Task> onDisconnected = _ =>
            {
                disconnected.TrySetResult();
                return Task.CompletedTask;
            };
            client.DisconnectedAsync += onDisconnected;
            try
            {
                await client.ConnectAsync(clientOptions, stoppingToken);
                // Values that arrive retained from here on predate this moment.
                _listeningSince = time.GetUtcNow();
                await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter("saic/+/vehicles/#", MqttQualityOfServiceLevel.AtMostOnce)
                    .WithTopicFilter("homeassistant/+/+/+/config", MqttQualityOfServiceLevel.AtMostOnce)
                    .Build(), stoppingToken);
                _connected = true;
                outage.Recovered();
                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                outage.Failed(ex);
            }
            finally
            {
                _connected = false;
                client.DisconnectedAsync -= onDisconnected;
                if (client.IsConnected)
                    await client.DisconnectAsync(cancellationToken: CancellationToken.None);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal void Receive(string topic, string payload, bool retained)
    {
        if (topic.StartsWith("homeassistant/", StringComparison.Ordinal))
            ReceiveDiscovery(topic, payload);
        else
            ReceiveValue(topic, payload, retained);
    }

    private void ReceiveValue(string topic, string payload, bool retained)
    {
        var parts = topic.Split('/');
        if (parts.Length < 5 || parts[0] != "saic" || parts[2] != "vehicles" || !VinFormat.IsValid(parts[3])) return;
        // Commands and their answers, MGMate's own and any other client's, are not the car's values.
        if (parts[^1] is "set" or "result") return;

        var subtopic = string.Join('/', parts[4..]);
        var state = _vehicles.GetOrAdd(parts[3], _ => new VehicleState());
        if (payload.Length == 0)
            state.Values.TryRemove(subtopic, out _);
        else
            state.Values[subtopic] = new GatewayValue(payload, retained ? null : time.GetUtcNow());
    }

    // homeassistant/{component}/{VIN}_mg/{unique id}/config, as the gateway names them.
    private void ReceiveDiscovery(string topic, string payload)
    {
        var parts = topic.Split('/');
        if (parts.Length != 5 || !parts[2].EndsWith("_mg", StringComparison.Ordinal)) return;
        var vin = parts[2][..^3];
        if (!VinFormat.IsValid(vin)) return;

        var state = _vehicles.GetOrAdd(vin, _ => new VehicleState());
        var id = parts[3];
        if (payload.Length == 0)
        {
            state.Entities.TryRemove(id, out _);
            return;
        }

        try
        {
            if (Parse(parts[1], id, vin, payload, out var device) is { } entity)
            {
                state.Entities[id] = entity;
                if (device is not null) state.Device = device;
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Unreadable discovery config on {Topic}", LogRedaction.MqttTopic(topic));
        }
    }

    // The config keys that name a topic: the state topic, and one or more command topics.
    private static readonly string[] CommandKeys =
    [
        "command_topic", "mode_command_topic", "temperature_command_topic", "fan_mode_command_topic",
        "preset_mode_command_topic", "swing_mode_command_topic", "power_command_topic",
    ];

    // Attributes the Debug page uses to offer a control or show a value; everything else is noise.
    private static readonly string[] AttributeKeys =
    [
        "options", "min", "max", "step", "mode", "pattern", "payload_on", "payload_off", "payload_lock",
        "payload_unlock", "state_locked", "state_unlocked", "payload_press", "modes", "min_temp", "max_temp",
        "temp_step", "temperature_unit", "mode_state_topic", "current_temperature_topic",
        "temperature_state_topic", "json_attributes_topic", "state_class", "precision",
        "command_template", "mode_command_template", "temperature_command_template",
        "mode_state_template", "temperature_state_template", "current_temperature_template",
    ];

    internal static GatewayEntity? Parse(string component, string id, string vin, string payload, out GatewayDevice? device)
    {
        device = null;
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        var prefix = $"/vehicles/{vin}/";
        string? Subtopic(string? full)
        {
            if (full is null) return null;
            var at = full.IndexOf(prefix, StringComparison.Ordinal);
            return at < 0 ? null : full[(at + prefix.Length)..];
        }
        string? Str(string key) => root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

        if (root.TryGetProperty("device", out var dev) && dev.ValueKind == JsonValueKind.Object)
        {
            string? D(string key) => dev.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
            string? sw = null;
            if ((root.TryGetProperty("o", out var origin) || root.TryGetProperty("origin", out origin))
                && origin.ValueKind == JsonValueKind.Object && origin.TryGetProperty("sw", out var swEl)
                && swEl.ValueKind == JsonValueKind.String)
                sw = swEl.GetString();
            device = new GatewayDevice(D("name"), D("manufacturer"), D("model"), D("hw_version"), sw);
        }

        var commands = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in CommandKeys)
            if (Subtopic(Str(key)) is { } sub)
                commands[key] = sub.EndsWith("/set", StringComparison.Ordinal) ? sub[..^4] : sub;

        var attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var key in AttributeKeys)
            if (root.TryGetProperty(key, out var el))
                attributes[key] = key.EndsWith("_topic", StringComparison.Ordinal) && el.ValueKind == JsonValueKind.String
                    ? JsonSerializer.SerializeToElement(Subtopic(el.GetString()))
                    : el.Clone();

        var stateTopic = Subtopic(Str("state_topic"))
            ?? Subtopic(Str("current_temperature_topic"))
            ?? Subtopic(Str("json_attributes_topic"));

        return new GatewayEntity(
            Id: id,
            Component: component,
            Name: Str("name") ?? id,
            EntityCategory: Str("entity_category"),
            EnabledByDefault: !root.TryGetProperty("enabled_by_default", out var enabled) || enabled.ValueKind != JsonValueKind.False,
            DeviceClass: Str("device_class"),
            Unit: Str("unit_of_measurement"),
            Icon: Str("icon"),
            StateSubtopic: stateTopic,
            ValueTemplate: Str("value_template"),
            CommandSubtopics: commands,
            Attributes: attributes);
    }
}
