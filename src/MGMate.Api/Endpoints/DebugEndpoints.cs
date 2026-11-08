using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MGMate.Api.Services;
using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;

namespace MGMate.Api.Endpoints;

/// <summary>
/// The Debug page: every entity the gateway announces for a car, with its newest value and what
/// MGMate keeps of it, and a way to send any command the gateway announced.
/// </summary>
public static class DebugEndpoints
{
    // A command value is a word, a number or a time; nothing longer is ever valid.
    private const int MaxValueLength = 64;

    public static IEndpointRouteBuilder MapDebugEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vehicles/{vin}/debug")
            .WithTags("Debug")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveVehicleFilter>();

        group.MapGet("/", (HttpContext httpContext, IGatewayCatalog catalog) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            return Results.Ok(Build(catalog.Get(vehicle.Vin), catalog.Connected, vehicle.SaicUser is not null));
        })
        .WithSummary("Everything the gateway announces and publishes for a vehicle");

        group.MapPost("/commands", async (
            HttpContext httpContext,
            DebugCommandRequest request,
            IGatewayCatalog catalog,
            IMqttPublisher mqtt,
            VehicleCommandGate gate,
            CancellationToken ct) =>
        {
            var vehicle = httpContext.ResolvedVehicle();
            if (vehicle.SaicUser is null)
                return ApiProblems.Problem(StatusCodes.Status409Conflict, VehicleCommandSender.AccountUnknownCode,
                    "SAIC username not yet known for this vehicle");

            var entity = catalog.Get(vehicle.Vin).Entities.FirstOrDefault(e => e.Id == request.EntityId);
            if (entity is null || request.Command is null || !entity.CommandSubtopics.TryGetValue(request.Command, out var subtopic))
                return ApiProblems.BadRequest("debug.commandUnknown", "The gateway has not announced this command for this car");

            if (Prepare(entity, request.Command, request.Value) is not { } payload)
                return ApiProblems.BadRequest("debug.valueInvalid", "This value is not one the command takes");

            var topic = $"saic/{vehicle.SaicUser}/vehicles/{vehicle.Vin}/{subtopic}/set";
            await gate.RunAsync(vehicle.Vin, subtopic, () => mqtt.PublishAsync(topic, payload, ct), ct);
            return Results.Ok(new DebugCommandResponse(subtopic, payload));
        })
        .WithSummary("Send a command the gateway announced for this vehicle");

        return app;
    }

    internal static DebugResponse Build(GatewayVehicleCatalog catalog, bool connected, bool canSend)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var entities = new List<DebugEntityDto>();

        foreach (var e in catalog.Entities)
        {
            GatewayValue? main = null;
            if (e.StateSubtopic is { } sub)
            {
                used.Add(sub);
                catalog.Values.TryGetValue(sub, out main);
            }

            // A climate entity reads its mode and target from topics of their own.
            var states = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (topicKey, templateKey, name) in ExtraStates)
            {
                if (Str(e, topicKey) is not { } extra) continue;
                used.Add(extra);
                if (catalog.Values.TryGetValue(extra, out var v))
                    states[name] = GatewayTemplate.Render(Str(e, templateKey), v.Payload) ?? v.Payload;
            }

            var commands = e.CommandSubtopics
                .Select(c => new DebugCommandDto(c.Key, c.Value, Understood(Str(e, TemplateKeyFor(c.Key)))))
                .ToList();

            entities.Add(new DebugEntityDto(
                e.Id, e.Component, e.Name, GroupOf(e), e.EnabledByDefault, e.DeviceClass, e.Unit, e.Icon,
                e.StateSubtopic,
                main?.Payload,
                main is null ? null : GatewayTemplate.Render(e.ValueTemplate, main.Payload) ?? main.Payload,
                main?.ReceivedAt,
                states,
                e.StateSubtopic is null ? null : Kept(e.StateSubtopic),
                commands,
                Attributes(e)));
        }

        var other = catalog.Values
            .Where(v => !used.Contains(v.Key))
            .OrderBy(v => v.Key, StringComparer.Ordinal)
            .Select(v => new DebugValueDto(v.Key, v.Value.Payload, v.Value.ReceivedAt, Kept(v.Key)))
            .ToList();

        return new DebugResponse(connected, canSend, catalog.ListeningSince, catalog.Device, entities, other);
    }

    private static readonly (string Topic, string Template, string Name)[] ExtraStates =
    [
        ("mode_state_topic", "mode_state_template", "mode"),
        ("temperature_state_topic", "temperature_state_template", "targetTemperature"),
    ];

    // As the page reads it: history, latest or notKept.
    private static string Kept(string subtopic) => GatewayDataCoverage.Classify(subtopic) switch
    {
        GatewayDataKept.History => "history",
        GatewayDataKept.Latest => "latest",
        _ => "notKept",
    };

    private static string GroupOf(GatewayEntity e) => e.EntityCategory switch
    {
        "config" => "configuration",
        "diagnostic" => "diagnostic",
        _ => e.CommandSubtopics.Count > 0 ? "controls" : "sensors",
    };

    // command_topic is shaped by command_template, mode_command_topic by mode_command_template.
    private static string TemplateKeyFor(string commandKey) => commandKey[..^"_topic".Length] + "_template";

    private static bool Understood(string? template) =>
        GatewayTemplate.Render(template, "0") is not null || GatewayTemplate.Render(template, "x") is not null;

    private static string? Str(GatewayEntity e, string key) =>
        e.Attributes.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static double? Num(GatewayEntity e, string key) =>
        e.Attributes.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetDouble() : null;

    private static string[] List(GatewayEntity e, string key) =>
        e.Attributes.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Array
            ? [.. el.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : x.GetRawText())]
            : [];

    // What the page needs to draw a control: the choices or limits, and the on/off words.
    private static Dictionary<string, JsonElement> Attributes(GatewayEntity e) =>
        e.Attributes
            .Where(a => !a.Key.EndsWith("_template", StringComparison.Ordinal) && !a.Key.EndsWith("_topic", StringComparison.Ordinal))
            .ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal);

    /// <summary>
    /// The payload to publish for <paramref name="value"/>, as Home Assistant would: checked
    /// against what the entity says it takes, then shaped by its command template. Null when the
    /// value is not one it takes, or the template is not one understood here.
    /// </summary>
    internal static string? Prepare(GatewayEntity e, string command, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxValueLength || value.Any(char.IsControl))
            return null;

        var valid = (e.Component, command) switch
        {
            ("switch", "command_topic") => value == (Str(e, "payload_on") ?? "ON") || value == (Str(e, "payload_off") ?? "OFF"),
            ("lock", "command_topic") => value == (Str(e, "payload_lock") ?? "LOCK") || value == (Str(e, "payload_unlock") ?? "UNLOCK"),
            ("button", "command_topic") => value == (Str(e, "payload_press") ?? "PRESS"),
            ("select", "command_topic") => List(e, "options").Contains(value),
            ("number", "command_topic") => InRange(value, Num(e, "min") ?? 1, Num(e, "max") ?? 100),
            ("text", "command_topic") => TextFits(e, value),
            ("climate", "mode_command_topic") => List(e, "modes").Contains(value),
            ("climate", "temperature_command_topic") => InRange(value, Num(e, "min_temp") ?? 7, Num(e, "max_temp") ?? 35),
            _ => false,
        };
        return valid ? GatewayTemplate.Render(Str(e, TemplateKeyFor(command)), value) : null;
    }

    private static bool InRange(string value, double min, double max) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
        && double.IsFinite(n) && n >= min && n <= max;

    private static bool TextFits(GatewayEntity e, string value)
    {
        if (value.Length < (Num(e, "min") ?? 0) || value.Length > (Num(e, "max") ?? 255)) return false;
        if (Str(e, "pattern") is not { } pattern) return true;
        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            return false;
        }
    }
}

public sealed record DebugCommandRequest(string? EntityId, string? Command, string? Value);

public sealed record DebugCommandResponse(string Topic, string Payload);

/// <summary>One command an entity takes: its kind (command_topic, mode_command_topic, …), its gateway topic, and whether the Debug page can shape its value.</summary>
public sealed record DebugCommandDto(string Key, string Topic, bool Supported);

public sealed record DebugEntityDto(
    string Id,
    string Component,
    string Name,
    string Group,
    bool EnabledByDefault,
    string? DeviceClass,
    string? Unit,
    string? Icon,
    string? Topic,
    string? RawValue,
    string? Value,
    DateTimeOffset? ReceivedAt,
    IReadOnlyDictionary<string, string> States,
    string? Kept,
    IReadOnlyList<DebugCommandDto> Commands,
    IReadOnlyDictionary<string, JsonElement> Attributes);

public sealed record DebugValueDto(string Topic, string Value, DateTimeOffset? ReceivedAt, string Kept);

public sealed record DebugResponse(
    bool Connected,
    bool CanSend,
    DateTimeOffset ListeningSince,
    GatewayDevice? Device,
    IReadOnlyList<DebugEntityDto> Entities,
    IReadOnlyList<DebugValueDto> OtherValues);
