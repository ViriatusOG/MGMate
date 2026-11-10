using System.Text.Json.Nodes;
using MGMate.Data.Demo;

namespace MGMate.Api.Services;

/// <summary>
/// Demo mode's Debug page: a fixed sample of what the gateway announces for an MG4, built from
/// discovery configs shaped like the gateway's own, with values to match the demo car.
/// </summary>
public sealed class DemoGatewayCatalog(GatewayVehicleCatalog catalog) : IGatewayCatalog
{
    public bool Connected => true;

    public GatewayVehicleCatalog Get(string vin) =>
        vin == DemoVehicleRepository.DemoVehicle.Vin
            ? catalog
            : new GatewayVehicleCatalog(null, [], new Dictionary<string, GatewayValue>(), catalog.ListeningSince);

    public static DemoGatewayCatalog Create(ILogger logger)
    {
        var vin = DemoVehicleRepository.DemoVehicle.Vin;
        var now = DateTimeOffset.UtcNow;
        var root = $"saic/demo@example.com/vehicles/{vin}/";
        var device = new JsonObject
        {
            ["name"] = $"MG MG4 Electric {vin}",
            ["manufacturer"] = "MG",
            ["model"] = "MG4 Electric 2026 Grey",
            ["hw_version"] = "EH32 S",
            ["identifiers"] = new JsonArray(vin),
        };

        var entities = new List<GatewayEntity>();
        var values = new Dictionary<string, GatewayValue>(StringComparer.Ordinal);
        GatewayDevice? gatewayDevice = null;

        void Add(string component, string name, string topic, JsonObject? extra = null, bool command = false)
        {
            var id = $"{vin}_{name.ToLowerInvariant().Replace(' ', '_')}";
            var config = new JsonObject
            {
                ["name"] = name,
                ["unique_id"] = id,
                ["device"] = device.DeepClone(),
                ["o"] = new JsonObject { ["name"] = "SAIC Python MQTT Gateway", ["sw"] = "0.14.0-rc2" },
                ["state_topic"] = root + topic,
            };
            if (command) config["command_topic"] = root + topic + "/set";
            foreach (var (k, v) in extra ?? [])
                config[k] = v?.DeepClone() switch
                {
                    JsonValue s when s.TryGetValue<string>(out var str) && str.StartsWith('@') => root + str[1..],
                    var other => other,
                };
            if (GatewayCatalog.Parse(component, id, vin, config.ToJsonString(), out var dev) is { } entity)
            {
                entities.Add(entity);
                gatewayDevice ??= dev;
            }
        }

        void Value(string topic, string payload, int minutesAgo = -1) =>
            values[topic] = new GatewayValue(payload, minutesAgo < 0 ? null : now.AddMinutes(-minutesAgo));

        JsonObject O(params (string Key, JsonNode? Value)[] pairs)
        {
            var o = new JsonObject();
            foreach (var (k, v) in pairs) o[k] = v;
            return o;
        }

        // Controls
        Add("lock", "Doors Lock", "doors/locked", O(("payload_lock", "True"), ("payload_unlock", "False"), ("state_locked", "True"), ("state_unlocked", "False"), ("icon", "mdi:car-door-lock")), command: true);
        Add("lock", "Boot Lock", "doors/boot", O(("payload_lock", "False"), ("payload_unlock", "True"), ("state_locked", "False"), ("state_unlocked", "True")), command: true);
        Add("switch", "Charging", "drivetrain/charging", O(("payload_on", "True"), ("payload_off", "False")), command: true);
        Add("number", "Target SoC", "drivetrain/socTarget", O(("min", 40), ("max", 100), ("step", 10), ("unit_of_measurement", "%"), ("mode", "slider")), command: true);
        Add("select", "Charge current limit", "drivetrain/chargeCurrentLimit", O(("options", new JsonArray("6A", "8A", "16A", "MAX"))), command: true);
        Add("lock", "Charging Cable Lock", "drivetrain/chargingCableLock", O(("payload_lock", "True"), ("payload_unlock", "False"), ("state_locked", "True"), ("state_unlocked", "False")), command: true);
        Add("switch", "Battery heating", "drivetrain/batteryHeating", O(("payload_on", "True"), ("payload_off", "False")), command: true);
        Add("switch", "Find my car", "location/findMyCar", O(("payload_on", "activate"), ("payload_off", "stop")), command: true);
        Add("switch", "Rear window defroster heating", "climate/rearWindowDefrosterHeating", O(("payload_on", "on"), ("payload_off", "off")), command: true);
        Add("switch", "Steering wheel heating", "climate/steeringWheelHeating", O(("payload_on", "on"), ("payload_off", "off")), command: true);
        Add("switch", "Front window defroster heating", "climate/remoteClimateState", O(("payload_on", "front"), ("payload_off", "off"),
            ("value_template", "{% if value == \"front\" %}front{% else %}off{% endif %}")), command: true);
        Add("climate", "Vehicle climate", "climate/interiorTemperature", O(
            ("mode_state_topic", "@climate/remoteClimateState"), ("mode_command_topic", "@climate/remoteClimateState/set"),
            ("mode_state_template", "{% if value == \"on\" %}auto{% else %}off{% endif %}"),
            ("mode_command_template", "{% if value == \"auto\" %}on{% else %}off{% endif %}"),
            ("modes", new JsonArray("off", "auto")),
            ("temperature_command_topic", "@climate/remoteTemperature/set"), ("temperature_command_template", "{{ value | int }}"),
            ("temperature_state_topic", "@climate/remoteTemperature"), ("min_temp", 16), ("max_temp", 28), ("temperature_unit", "C")));
        Add("select", "Scheduled Charging Mode", "drivetrain/chargingSchedule", O(
            ("options", new JsonArray("DISABLED", "UNTIL_CONFIGURED_TIME", "UNTIL_CONFIGURED_SOC")),
            ("value_template", "{{ value_json[\"mode\"] }}"),
            ("command_template", "{\"startTime\": \"{{ states('sensor.start') }}\", \"mode\": \"{{ value }}\"}")), command: true);

        // Sensors
        Add("sensor", "SoC", "drivetrain/soc", O(("unit_of_measurement", "%"), ("device_class", "battery")));
        Add("sensor", "Range", "drivetrain/range", O(("unit_of_measurement", "km"), ("device_class", "distance")));
        Add("sensor", "Power", "drivetrain/power", O(("unit_of_measurement", "kW"), ("device_class", "power")));
        Add("sensor", "Voltage", "drivetrain/voltage", O(("unit_of_measurement", "V"), ("device_class", "voltage")));
        Add("sensor", "Current", "drivetrain/current", O(("unit_of_measurement", "A"), ("device_class", "current")));
        Add("sensor", "Mileage", "drivetrain/mileage", O(("unit_of_measurement", "km"), ("device_class", "distance")));
        Add("sensor", "Auxiliary battery voltage", "drivetrain/auxiliaryBatteryVoltage", O(("unit_of_measurement", "V"), ("device_class", "voltage")));
        Add("sensor", "Remaining charging time", "drivetrain/remainingChargingTime", O(("unit_of_measurement", "s"), ("device_class", "duration")));
        Add("binary_sensor", "Charger connected", "drivetrain/chargerConnected", O(("payload_on", "True"), ("payload_off", "False"), ("device_class", "plug")));
        Add("binary_sensor", "Vehicle Running", "drivetrain/running", O(("payload_on", "True"), ("payload_off", "False"), ("device_class", "running")));
        foreach (var (name, topic) in new[] { ("Door driver", "doors/driver"), ("Door passenger", "doors/passenger"), ("Door rear left", "doors/rearLeft"), ("Door rear right", "doors/rearRight"), ("Bonnet", "doors/bonnet"), ("Boot", "doors/boot") })
            Add("binary_sensor", name, topic, O(("payload_on", "True"), ("payload_off", "False"), ("device_class", "door")));
        foreach (var (name, topic) in new[] { ("Window driver", "windows/driver"), ("Window passenger", "windows/passenger"), ("Window rear left", "windows/rearLeft"), ("Window rear right", "windows/rearRight") })
            Add("binary_sensor", name, topic, O(("payload_on", "True"), ("payload_off", "False"), ("device_class", "window")));
        foreach (var (name, topic) in new[] { ("Tyres front left pressure", "tyres/frontLeftPressure"), ("Tyres front right pressure", "tyres/frontRightPressure"), ("Tyres rear left pressure", "tyres/rearLeftPressure"), ("Tyres rear right pressure", "tyres/rearRightPressure") })
            Add("sensor", name, topic, O(("unit_of_measurement", "bar"), ("device_class", "pressure")));
        Add("sensor", "Interior temperature", "climate/interiorTemperature", O(("unit_of_measurement", "°C"), ("device_class", "temperature")));
        Add("sensor", "Exterior temperature", "climate/exteriorTemperature", O(("unit_of_measurement", "°C"), ("device_class", "temperature")));
        Add("binary_sensor", "Lights Dipped Beam", "lights/dippedBeam", O(("payload_on", "True"), ("payload_off", "False"), ("device_class", "light")));
        Add("sensor", "Energy Usage of the Day", "drivetrain/powerUsageOfDay", O(("unit_of_measurement", "kWh"), ("enabled_by_default", false)));

        // Configuration and diagnostics
        Add("select", "Gateway refresh mode", "refresh/mode", O(("options", new JsonArray("periodic", "off", "force")), ("entity_category", "config")), command: true);
        Add("number", "Gateway active refresh period", "refresh/period/active", O(("min", 30), ("max", 3600), ("step", 1), ("unit_of_measurement", "s"), ("entity_category", "config")), command: true);
        Add("number", "Gateway cached status refresh period", "refresh/period/cachedStatus", O(("min", 0), ("max", 86400), ("step", 1), ("unit_of_measurement", "s"), ("entity_category", "config")), command: true);
        Add("sensor", "Last vehicle status time", "refresh/lastStatusTime", O(("device_class", "timestamp"), ("entity_category", "diagnostic")));
        Add("sensor", "Last poll error", "refresh/lastError", O(("entity_category", "diagnostic")));
        Add("sensor", "Gateway polling phase", "refresh/pollingPhase", O(("entity_category", "diagnostic")));
        Add("sensor", "BMS Charge Status", "bms/chargeStatus", O(("entity_category", "diagnostic"), ("enabled_by_default", false)));
        Add("sensor", "OBC Power Three Phase", "obc/powerThreePhase", O(("unit_of_measurement", "W"), ("entity_category", "diagnostic"), ("enabled_by_default", false)));

        Value("doors/locked", "True", 3); Value("doors/boot", "False", 3); Value("drivetrain/charging", "False", 2);
        Value("drivetrain/socTarget", "80"); Value("drivetrain/chargeCurrentLimit", "MAX"); Value("drivetrain/chargingCableLock", "True");
        Value("drivetrain/batteryHeating", "False", 2); Value("climate/rearWindowDefrosterHeating", "off", 3);
        Value("climate/steeringWheelHeating", "off", 3);
        Value("climate/remoteClimateState", "off", 3); Value("climate/remoteTemperature", "22");
        Value("drivetrain/chargingSchedule", """{"mode": "DISABLED", "startTime": "00:30", "endTime": "05:30"}""");
        Value("drivetrain/soc", "78", 2); Value("drivetrain/range", "319", 2); Value("drivetrain/power", "0.0", 2);
        Value("drivetrain/voltage", "398.6", 2); Value("drivetrain/current", "0.0", 2); Value("drivetrain/mileage", "8412.0", 2);
        Value("drivetrain/auxiliaryBatteryVoltage", "12.6", 2); Value("drivetrain/chargerConnected", "False", 2);
        Value("drivetrain/running", "False", 2);
        foreach (var t in new[] { "doors/driver", "doors/passenger", "doors/rearLeft", "doors/rearRight", "doors/bonnet", "windows/driver", "windows/passenger", "windows/rearLeft", "windows/rearRight", "lights/dippedBeam" })
            Value(t, "False", 3);
        Value("tyres/frontLeftPressure", "2.5", 3); Value("tyres/frontRightPressure", "2.5", 3);
        Value("tyres/rearLeftPressure", "2.4", 3); Value("tyres/rearRightPressure", "2.5", 3);
        Value("climate/interiorTemperature", "14", 3); Value("climate/exteriorTemperature", "9", 3);
        Value("refresh/mode", "periodic"); Value("refresh/period/active", "30"); Value("refresh/period/cachedStatus", "120");
        Value("refresh/lastStatusTime", now.AddMinutes(-2).ToString("O"), 2); Value("refresh/lastError", "None");
        Value("refresh/pollingPhase", "inactive", 2); Value("bms/chargeStatus", "NOT_CHARGING", 2);
        Value("location/position", """{"latitude": 52.4862, "longitude": -1.8904, "speed": 0}""", 3);
        Value("location/heading", "184", 3); Value("info/brand", "MG"); Value("info/year", "2026"); Value("info/color", "Grey");
        Value("drivetrain/hybrid_electrical_range", "0", 2);

        logger.LogInformation("Demo Debug page: {Count} sample gateway entities", entities.Count);
        return new DemoGatewayCatalog(new GatewayVehicleCatalog(
            gatewayDevice, [.. entities.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)], values, now.AddHours(-1)));
    }
}
