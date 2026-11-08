using System.Text.Json.Nodes;
using MGMate.Api.Endpoints;
using MGMate.Api.Services;
using MGMate.Core.Configuration;
using MGMate.Core.Helpers;
using MGMate.Core.Models;
using MGMate.Worker.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MGMate.Tests;

public class GatewayTemplateTests
{
    [Theory]
    [InlineData(null, "front", "front")]
    [InlineData("{{ value }}", "MAX", "MAX")]
    [InlineData("{{ value | int }}", "21.7", "21")]
    [InlineData("""{{ value_json["mode"] }}""", """{"mode":"DISABLED","startTime":"00:30"}""", "DISABLED")]
    [InlineData("""{{ value_json["distance"] | int(0) }}""", """{"distance":12.9}""", "12")]
    [InlineData("""{% if value == "front" %}front{% else %}off{% endif %}""", "front", "front")]
    [InlineData("""{% if value == "front" %}front{% else %}off{% endif %}""", "on", "off")]
    [InlineData("""{% if value == "auto" %}on{% else %}off{% endif %}""", "auto", "on")]
    [InlineData("""{% set v = value | int %}{% if v == 0 %}OFF{% elif v == 1 %}LOW{% elif v == 2 %}MEDIUM{% else %}HIGH{% endif %}""", "2", "MEDIUM")]
    [InlineData("""{% if value == "OFF" %}0{% elif value == "LOW" %}1{% elif value == "MEDIUM" %}2{% else %}3{% endif %}""", "LOW", "1")]
    public void Render_UnderstoodShapes(string? template, string value, string expected) =>
        Assert.Equal(expected, GatewayTemplate.Render(template, value));

    [Theory]
    // The schedule commands fill in other entities' states, which only Home Assistant knows.
    [InlineData("""{"startTime": "{{ value }}", "mode": "{{ states('sensor.mode') }}"}""", "00:30")]
    [InlineData("{{ (now() + timedelta(seconds = value | int)).isoformat() }}", "600")]
    [InlineData("{{ value | int }}", "abc")]
    public void Render_OtherShapes_AreNotUnderstood(string template, string value) =>
        Assert.Null(GatewayTemplate.Render(template, value));
}

public class GatewayCatalogTests
{
    private const string Vin = "LSJA24U90PS123456";
    private const string Root = $"saic/me@example.com/vehicles/{Vin}/";

    private static string Config(string name, JsonObject extra)
    {
        var config = new JsonObject
        {
            ["name"] = name,
            ["device"] = new JsonObject { ["name"] = "MG MG4", ["manufacturer"] = "MG", ["model"] = "MG4 2026", ["hw_version"] = "EH32 S", ["identifiers"] = new JsonArray(Vin) },
            ["o"] = new JsonObject { ["sw"] = "0.14.0-rc1" },
        };
        foreach (var (k, v) in extra) config[k] = v?.DeepClone();
        return config.ToJsonString();
    }

    private static GatewayCatalog NewCatalog() =>
        new(Options.Create(new MqttOptions()), NullLogger<GatewayCatalog>.Instance);

    private static GatewayCatalog Sample()
    {
        var catalog = NewCatalog();
        catalog.Receive($"homeassistant/lock/{Vin}_mg/{Vin}_doors_lock/config", Config("Doors Lock", new JsonObject
        {
            ["state_topic"] = Root + "doors/locked",
            ["command_topic"] = Root + "doors/locked/set",
            ["payload_lock"] = "True",
            ["payload_unlock"] = "False",
        }), retained: true);
        catalog.Receive($"homeassistant/select/{Vin}_mg/{Vin}_heated_seat_front_left_level/config", Config("Heated Seat Front Left Level", new JsonObject
        {
            ["state_topic"] = Root + "climate/heatedSeatsFrontLeftLevel",
            ["command_topic"] = Root + "climate/heatedSeatsFrontLeftLevel/set",
            ["options"] = new JsonArray("OFF", "LOW", "MEDIUM", "HIGH"),
            ["value_template"] = """{% set v = value | int %}{% if v == 0 %}OFF{% elif v == 1 %}LOW{% elif v == 2 %}MEDIUM{% else %}HIGH{% endif %}""",
            ["command_template"] = """{% if value == "OFF" %}0{% elif value == "LOW" %}1{% elif value == "MEDIUM" %}2{% else %}3{% endif %}""",
        }), retained: true);
        catalog.Receive($"homeassistant/select/{Vin}_mg/{Vin}_scheduled_charging_mode/config", Config("Scheduled Charging Mode", new JsonObject
        {
            ["state_topic"] = Root + "drivetrain/chargingSchedule",
            ["command_topic"] = Root + "drivetrain/chargingSchedule/set",
            ["options"] = new JsonArray("DISABLED", "UNTIL_CONFIGURED_TIME"),
            ["command_template"] = """{"mode": "{{ value }}", "startTime": "{{ states('sensor.x') }}"}""",
        }), retained: true);
        catalog.Receive($"homeassistant/number/{Vin}_mg/{Vin}_gateway_active_refresh_period/config", Config("Gateway active refresh period", new JsonObject
        {
            ["state_topic"] = Root + "refresh/period/active",
            ["command_topic"] = Root + "refresh/period/active/set",
            ["entity_category"] = "config",
            ["min"] = 30,
            ["max"] = 3600,
        }), retained: true);
        catalog.Receive($"homeassistant/sensor/{Vin}_mg/{Vin}_soc/config", Config("SoC", new JsonObject
        {
            ["state_topic"] = Root + "drivetrain/soc",
            ["unit_of_measurement"] = "%",
        }), retained: true);
        // Another device's discovery, and a command published by someone: neither is the car's.
        catalog.Receive("homeassistant/sensor/0x00158d/temperature/config", """{"name":"Garden","state_topic":"zigbee/garden"}""", retained: true);
        catalog.Receive(Root + "doors/locked/set", "False", retained: false);

        catalog.Receive(Root + "doors/locked", "True", retained: true);
        catalog.Receive(Root + "climate/heatedSeatsFrontLeftLevel", "2", retained: true);
        catalog.Receive(Root + "drivetrain/soc", "78", retained: false);
        catalog.Receive(Root + "info/color", "Grey", retained: true);
        return catalog;
    }

    [Fact]
    public void Build_GroupsEntitiesAndShowsValues()
    {
        var catalog = Sample();
        var response = DebugEndpoints.Build(catalog.Get(Vin), connected: true, canSend: true);

        Assert.Equal(["Doors Lock", "Gateway active refresh period", "Heated Seat Front Left Level", "Scheduled Charging Mode", "SoC"],
            response.Entities.Select(e => e.Name));
        var byName = response.Entities.ToDictionary(e => e.Name);
        Assert.Equal("controls", byName["Doors Lock"].Group);
        Assert.Equal("configuration", byName["Gateway active refresh period"].Group);
        Assert.Equal("sensors", byName["SoC"].Group);
        Assert.Equal("MEDIUM", byName["Heated Seat Front Left Level"].Value);
        Assert.Equal("2", byName["Heated Seat Front Left Level"].RawValue);
        Assert.Null(byName["Doors Lock"].ReceivedAt);
        Assert.NotNull(byName["SoC"].ReceivedAt);
        Assert.Equal("history", byName["SoC"].Kept);
        Assert.Equal("notKept", byName["Gateway active refresh period"].Kept);
        Assert.Equal("doors/locked", Assert.Single(byName["Doors Lock"].Commands).Topic);
        Assert.False(Assert.Single(byName["Scheduled Charging Mode"].Commands).Supported);

        var other = Assert.Single(response.OtherValues);
        Assert.Equal(("info/color", "latest"), (other.Topic, other.Kept));
        Assert.Equal("EH32 S", response.Device?.HardwareVersion);
        Assert.Equal("0.14.0-rc1", response.Device?.GatewayVersion);
    }

    [Fact]
    public void EmptyDiscoveryPayload_RemovesTheEntity()
    {
        var catalog = Sample();
        catalog.Receive($"homeassistant/sensor/{Vin}_mg/{Vin}_soc/config", "", retained: false);
        Assert.DoesNotContain(catalog.Get(Vin).Entities, e => e.Name == "SoC");
    }

    [Theory]
    [InlineData("Doors Lock", "False", "False")]
    [InlineData("Doors Lock", "open", null)]
    [InlineData("Heated Seat Front Left Level", "LOW", "1")]
    [InlineData("Heated Seat Front Left Level", "WARM", null)]
    [InlineData("Gateway active refresh period", "60", "60")]
    [InlineData("Gateway active refresh period", "5", null)]
    [InlineData("Scheduled Charging Mode", "DISABLED", null)]
    public void Prepare_ChecksTheValueAndShapesIt(string entity, string value, string? expected)
    {
        var e = Sample().Get(Vin).Entities.Single(x => x.Name == entity);
        Assert.Equal(expected, DebugEndpoints.Prepare(e, "command_topic", value));
    }
}

public class GatewayDataCoverageTests
{
    // The Debug page says a value has a history only if the Worker really stores it.
    [Fact]
    public void EveryTelemetrySubtopic_IsOneTheMapperStores()
    {
        foreach (var subtopic in GatewayDataCoverage.TelemetrySubtopics)
            Assert.True(TelemetryMapper.Map(new TelemetrySnapshot(), subtopic, "1") != TelemetryMapping.UnknownTopic, subtopic);
    }

    [Theory]
    [InlineData("drivetrain/soc", GatewayDataKept.History)]
    [InlineData("refresh/lastStatusTime", GatewayDataKept.History)]
    [InlineData("refresh/pollingPhase", GatewayDataKept.Latest)]
    [InlineData("info/configuration/BATTERY_TYPE", GatewayDataKept.Latest)]
    [InlineData("refresh/period/active", GatewayDataKept.NotKept)]
    public void Classify(string subtopic, GatewayDataKept expected) =>
        Assert.Equal(expected, GatewayDataCoverage.Classify(subtopic));

    [Fact]
    public void Mapper_StoresTheGatewayStatusTimesAndHeatingStopReason()
    {
        var s = new TelemetrySnapshot();
        Assert.True(TelemetryMapper.ApplyMessage(s, "refresh/lastStatusTime", "2026-11-08T08:21:00+00:00"));
        Assert.True(TelemetryMapper.ApplyMessage(s, "refresh/lastActivity", "2026-11-08T08:20:00+00:00"));
        Assert.True(TelemetryMapper.ApplyMessage(s, "refresh/lastCachedState", "2026-11-08T08:00:00+00:00"));
        Assert.True(TelemetryMapper.ApplyMessage(s, "drivetrain/batteryHeatingStopReason", "TARGET_REACHED"));
        Assert.Equal(new DateTime(2026, 11, 8, 8, 21, 0, DateTimeKind.Utc), s.LastStatusAt);
        Assert.Equal(new DateTime(2026, 11, 8, 8, 20, 0, DateTimeKind.Utc), s.LastActivityAt);
        Assert.Equal(new DateTime(2026, 11, 8, 8, 0, 0, DateTimeKind.Utc), s.LastCachedStateAt);
        Assert.Equal("TARGET_REACHED", s.BatteryHeatingStopReason);
    }
}
