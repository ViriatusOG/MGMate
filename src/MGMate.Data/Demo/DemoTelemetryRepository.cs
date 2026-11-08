using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;
using MGMate.Core.Models;

namespace MGMate.Data.Demo;

public sealed class DemoTelemetryRepository : ITelemetryRepository
{
    private static long _nextId = 9000;
    private static readonly object _lock = new();

    // What the demo ZS EV covers on a full battery. The electric range is derived from the state of
    // charge, so the two stay in step when the demo panel moves the state of charge.
    private const double FullBatteryRangeKm = 400.0;

    private static double ElectricRangeAt(double socPercent) =>
        Math.Round(socPercent / 100.0 * FullBatteryRangeKm);

    private TelemetrySnapshot _current = BuildDefaultSnapshot();

    // A little over a year, so the statistics have something to show for last year and per month.
    private const int HistoryDays = 400;

    private const int SlowLeakDays = 12;
    private const double SlowLeakBarPerDay = 0.02;

    private static readonly Lazy<IReadOnlyList<TelemetrySnapshot>> _history =
        new(BuildHistory, LazyThreadSafetyMode.ExecutionAndPublication);

    public Task<long> AddAsync(TelemetrySnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(Interlocked.Increment(ref _nextId));

    public Task MergeIntoAsync(long rowId, TelemetrySnapshot patch, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<TelemetrySnapshot?> GetMergedLatestAsync(int vehicleId, CancellationToken ct = default)
    {
        lock (_lock) { return Task.FromResult<TelemetrySnapshot?>(_current.Clone()); }
    }

    public Task<IReadOnlyList<DailyStat>> GetDailyStatsAsync(
        int vehicleId, DateOnly from, DateOnly to, TimeZoneInfo zone, DateOnly today, CancellationToken ct = default)
    {
        var builder = new DailyStatsBuilder(zone, from, to, today);
        foreach (var snapshot in _history.Value.OrderBy(s => s.RecordedAt))
            builder.Add(TelemetryHistoryPoint.FromSnapshot(snapshot));
        return Task.FromResult(builder.Build());
    }

    // DemoTripRepository serves the demo's trips from DemoTrips directly, so nothing asks the demo
    // telemetry for the fixes to cut them from.
    public Task<IReadOnlyList<TripPoint>> GetGpsFixesAsync(
        int vehicleId, DateTime from, DateTime to, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TripPoint>>([]);

    public Task<DateTime?> GetFirstGpsFixAtAsync(int vehicleId, CancellationToken ct = default) =>
        Task.FromResult<DateTime?>(null);

    // Only the Worker's recorder anchors trips, and demo mode runs no Worker.
    public Task<TripPoint?> GetLastGpsFixBeforeAsync(int vehicleId, DateTime before, CancellationToken ct = default) =>
        Task.FromResult<TripPoint?>(null);

    public Task<IReadOnlyList<DateTime>> GetReportTimesAsync(
        int vehicleId, DateTime after, DateTime before, int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DateTime>>([]);

    // Maintenance asks for the reading now, which is the live snapshot's. Past readings are only
    // asked for by the Worker's recorder, and demo mode runs no Worker: DemoTrips carries those.
    public Task<double?> GetOdometerAtAsync(int vehicleId, DateTime at, CancellationToken ct = default)
    {
        lock (_lock) { return Task.FromResult(_current.OdometerKm); }
    }

    // Demo trips carry their energy already; nothing reads these in demo mode.
    public Task<double?> GetMeanOutsideTemperatureAsync(int vehicleId, DateTime from, DateTime to, CancellationToken ct = default) =>
        Task.FromResult<double?>(null);

    public Task<TimedReading?> GetSocAtAsync(int vehicleId, DateTime at, CancellationToken ct = default) =>
        Task.FromResult<TimedReading?>(null);

    public Task<DayCounterReading?> GetDayCountersAtAsync(int vehicleId, DateTime at, CancellationToken ct = default) =>
        Task.FromResult<DayCounterReading?>(null);

    // Demo charges are fixed and carry their own chart; nothing records them.
    // Nothing records demo charges (DemoSeeder saves them), so their chart is drawn here: a
    // steady charge that tapers at the end, every five minutes, as the readings the window asks for.
    public Task<IReadOnlyList<ChargeSample>> GetChargeSamplesAsync(int vehicleId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var start = from.AddMinutes(5);
        var end = to - ChargeSegmenter.PauseThreshold;
        if (end <= start) return Task.FromResult<IReadOnlyList<ChargeSample>>([]);
        var dc = (end - start) < TimeSpan.FromHours(1.5);
        var steps = Math.Max(2, (int)((end - start).TotalMinutes / (dc ? 2 : 5)));
        var samples = new List<ChargeSample>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var share = (double)i / steps;
            var power = dc ? 95 - 55 * share : share < 0.85 ? 7.1 : 7.1 * (1 - share) / 0.15;
            samples.Add(new ChargeSample(
                start.AddMinutes((end - start).TotalMinutes * share), true, Math.Round(30 + 50 * share, 1), -power,
                dc ? "DC" : "AC", null, null, null, null, 80, Math.Round(11 - 2 * share + Math.Sin(i / 6.0) * 0.4, 1)));
        }
        return Task.FromResult<IReadOnlyList<ChargeSample>>(samples);
    }

    // Demo trips carry their route only, so what the car would have reported along it is drawn here:
    // speed, falling charge, power with regen on the slow-downs, rolling elevation and temperature.
    public Task<IReadOnlyList<TripTelemetryPoint>> GetTripTelemetryAsync(int vehicleId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var steps = Math.Max(2, (int)(to - from).TotalSeconds / 20);
        var points = new List<TripTelemetryPoint>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var share = (double)i / steps;
            var speed = Math.Max(0, 55 + 35 * Math.Sin(i / 9.0) + 10 * Math.Sin(i / 3.1)) * Math.Min(1, Math.Min(share, 1 - share) * 12);
            // A red light now and then, so the detail has stopped time to show.
            if (Math.Sin(i / 7.0) > 0.96) speed = 0;
            var accelerating = Math.Cos(i / 9.0) + 0.3 * Math.Cos(i / 3.1);
            var power = speed < 1 ? 0.4 : accelerating < -0.4 ? -12 * -accelerating : 6 + speed * 0.18 + accelerating * 8;
            points.Add(new TripTelemetryPoint(
                from.AddSeconds((to - from).TotalSeconds * share), Math.Round(speed, 1), Math.Round(72 - 6 * share, 1),
                Math.Round(power, 1), Math.Round(30 + 25 * Math.Sin(share * Math.PI * 2) + 8 * Math.Sin(i / 5.0), 1),
                Math.Round(9 + 2 * share, 1), true, ElectricRangeAt(72 - 6 * share)));
        }
        return Task.FromResult<IReadOnlyList<TripTelemetryPoint>>(points);
    }

    // The history's readings in the window, then the live status, so the overview's level line
    // and the activity page have something to draw.
    public Task<IReadOnlyList<ActivitySample>> GetActivitySamplesAsync(int vehicleId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var samples = _history.Value
            .Where(s => s.RecordedAt >= from && s.RecordedAt <= to)
            .OrderBy(s => s.RecordedAt)
            .Select(ToActivitySample)
            .ToList();
        TelemetrySnapshot now;
        lock (_lock) { now = _current.Clone(); }
        if (now.RecordedAt >= from && now.RecordedAt <= to) samples.Add(ToActivitySample(now));
        return Task.FromResult<IReadOnlyList<ActivitySample>>(samples);
    }

    private static ActivitySample ToActivitySample(TelemetrySnapshot s) => new(
        s.RecordedAt,
        s.IsLocked,
        s.DriverDoorOpen is null && s.PassengerDoorOpen is null && s.RearLeftDoorOpen is null && s.RearRightDoorOpen is null && s.TrunkOpen is null
            ? null
            : s.DriverDoorOpen == true || s.PassengerDoorOpen == true || s.RearLeftDoorOpen == true || s.RearRightDoorOpen == true || s.TrunkOpen == true,
        s.ChargerConnected,
        s.IsCharging,
        s.ClimateOn,
        s.EngineRunning,
        s.EvSocPercent);

    public Task<DateTime?> GetFirstChargingAtAsync(int vehicleId, CancellationToken ct = default) =>
        Task.FromResult<DateTime?>(null);

    // Only the Worker's parked alerts weigh past polls, and demo mode runs no Worker.
    public Task<IReadOnlyList<StatusReading>> GetStatusReadingsAsync(int vehicleId, int count, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StatusReading>>([]);

    public Task<string?> GetChargingStopReasonAsync(int vehicleId, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    // The generated history, oldest first, then the live snapshot the demo controls change.
    public async IAsyncEnumerable<TelemetrySnapshot> StreamAllAsync(
        int vehicleId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var snapshot in _history.Value.OrderBy(s => s.RecordedAt))
        {
            ct.ThrowIfCancellationRequested();
            yield return snapshot;
        }
        yield return (await GetMergedLatestAsync(vehicleId, ct))!;
    }

    public Task<PositionFix?> GetLatestPositionAsync(int vehicleId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_current is { Latitude: { } lat, Longitude: { } lon }
                ? new PositionFix(lat, lon, _current.Heading)
                : null);
        }
    }

    // Demo data is not ingested from MQTT, so there are no raw topics to report.

    public Task<VehicleAggregateStats> GetAggregateStatsAsync(
        int vehicleId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var history = _history.Value.Where(s => s.RecordedAt >= from && s.RecordedAt <= to).ToList();
        var known = history.Count(s => s.ClimateOn != null);
        var on = history.Count(s => s.ClimateOn == true);
        var pct = known > 0 ? (int?)Math.Round((double)on / known * 100) : null;
        return Task.FromResult(new VehicleAggregateStats(pct, on, known));
    }

    public void ApplyOverride(DemoStatusOverrideDto dto)
    {
        lock (_lock)
        {
            if (dto.IsLocked.HasValue) _current.IsLocked = dto.IsLocked;
            if (dto.EngineRunning.HasValue) _current.EngineRunning = dto.EngineRunning;
            if (dto.ClimateOn.HasValue)
            {
                _current.ClimateOn = dto.ClimateOn;
                _current.ClimateMode = dto.ClimateOn.Value ? "on" : "off";
            }
            if (dto.DriverDoorOpen.HasValue) _current.DriverDoorOpen = dto.DriverDoorOpen;
            if (dto.PassengerDoorOpen.HasValue) _current.PassengerDoorOpen = dto.PassengerDoorOpen;
            if (dto.RearLeftDoorOpen.HasValue) _current.RearLeftDoorOpen = dto.RearLeftDoorOpen;
            if (dto.RearRightDoorOpen.HasValue) _current.RearRightDoorOpen = dto.RearRightDoorOpen;
            if (dto.TrunkOpen.HasValue) _current.TrunkOpen = dto.TrunkOpen;
            if (dto.BonnetOpen.HasValue) _current.BonnetOpen = dto.BonnetOpen;
            if (dto.DriverWindowOpen.HasValue) _current.DriverWindowOpen = dto.DriverWindowOpen;
            if (dto.PassengerWindowOpen.HasValue) _current.PassengerWindowOpen = dto.PassengerWindowOpen;
            if (dto.RearLeftWindowOpen.HasValue) _current.RearLeftWindowOpen = dto.RearLeftWindowOpen;
            if (dto.RearRightWindowOpen.HasValue) _current.RearRightWindowOpen = dto.RearRightWindowOpen;
            if (dto.ChargerConnected.HasValue) _current.ChargerConnected = dto.ChargerConnected;
            if (dto.IsCharging.HasValue) _current.IsCharging = dto.IsCharging;
            if (dto.LightsMainBeam.HasValue) _current.LightsMainBeam = dto.LightsMainBeam;
            if (dto.LightsDippedBeam.HasValue) _current.LightsDippedBeam = dto.LightsDippedBeam;
            if (dto.LightsSide.HasValue) _current.LightsSide = dto.LightsSide;
            if (dto.EvSocPercent.HasValue)
            {
                _current.EvSocPercent = dto.EvSocPercent;
                _current.HvSocKwh = Math.Round(dto.EvSocPercent.Value / 100.0 * 70.0, 1);
                _current.ElectricRangeKm = ElectricRangeAt(dto.EvSocPercent.Value);
            }
            if (dto.InteriorTemperature.HasValue) _current.InteriorTemperature = dto.InteriorTemperature;
            if (dto.ExteriorTemperature.HasValue) _current.ExteriorTemperature = dto.ExteriorTemperature;
            if (dto.Speed.HasValue) _current.Speed = dto.Speed;
        }
    }

    /// <summary>
    /// Carries out a command the way the car would, on the demo car's state: the command name and
    /// value as <see cref="VehicleCommands"/> checked them. Returns why the car refused, the way the
    /// gateway reports it, or null when it carried the command out.
    /// </summary>
    public string? ApplyCommand(string command, string value)
    {
        lock (_lock)
        {
            var refusal = Apply(_current, command, value);
            if (refusal is null)
            {
                _current.RecordedAt = DateTime.UtcNow;
                _current.LastVehicleStateAt = _current.RecordedAt;
            }
            return refusal;
        }
    }

    // The car's side of each command. Horn and lights, opening the boot lock and a refresh change
    // nothing the dashboard shows beyond the reading's time, except the boot, which pops open.
    private static string? Apply(TelemetrySnapshot car, string command, string value)
    {
        switch (command)
        {
            case "climate":
                car.ClimateOn = value != "off";
                car.ClimateMode = value;
                break;
            case "climate-temperature":
                car.RemoteTemperature = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "rear-defroster":
                car.RearWindowDefroster = value == "on";
                break;
            case "seat-left":
                car.HeatedSeatFrontLeft = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "seat-right":
                car.HeatedSeatFrontRight = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "lock":
                car.IsLocked = value == "True";
                break;
            case "boot":
                car.TrunkOpen = true;
                break;
            case "charge-limit":
                car.ChargeCurrentLimit = value;
                break;
            case "charge-target":
                car.SocTargetPercent = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "charging" when value == "true" && car.ChargerConnected != true:
                return "Failed: the charging cable is not plugged in";
            case "charging":
                car.IsCharging = value == "true";
                car.BmsChargeStatus = value == "true" ? "CHARGING_1" : "CHARGING_STOPPED";
                break;
            case "charging-cable-lock":
                car.ChargingCableLock = value == "true";
                break;
            case "battery-heating":
                car.BatteryHeating = value == "true";
                break;
            case "scheduled-charging" when SafeJson.TryDeserialize<Dictionary<string, string>>(value) is { } schedule:
                car.ChargingScheduleMode = schedule["mode"];
                car.ChargingScheduleStartTime = schedule["startTime"];
                car.ChargingScheduleEndTime = schedule["endTime"];
                break;
            case "battery-heating-schedule" when SafeJson.TryDeserialize<Dictionary<string, string>>(value) is { } schedule:
                car.BatteryHeatingScheduleMode = schedule["mode"];
                car.BatteryHeatingScheduleStartTime = schedule["startTime"];
                break;
        }
        return null;
    }

    private static TelemetrySnapshot BuildDefaultSnapshot()
    {
        var current = DemoTrips.InProgressWaypoints[^1];

        return new()
        {
            Id = 1,
            VehicleId = 1,
            RecordedAt = DateTime.UtcNow,
            FuelLevelPercent = 68,
            FuelRangeKm = 420,
            EvSocPercent = 71,
            ElectricRangeKm = ElectricRangeAt(71),
            HvSocKwh = Math.Round(71.0 / 100.0 * 70.0, 1),
            HvTotalCapacityKwh = 70.0,
            HvVoltage = 386.0,
            HvCurrent = 42.0,
            HvPower = 16.2,
            HvBatteryActive = true,
            OdometerKm = DemoTrips.CurrentOdometerKm,
            EngineRunning = true,
            IsCharging = false,
            ChargerConnected = false,
            ChargingType = "AC",
            ChargingCableLock = false,
            BmsChargeStatus = "UNPLUGGED",
            SocTargetPercent = 80,
            ChargeCurrentLimit = "Max",
            OnboardChargerPlugStatus = 0,
            OffboardChargerPlugStatus = 0,
            ObcVoltage = 0.0,
            ObcCurrent = 0.0,
            ObcPowerSinglePhase = 0.0,
            InteriorTemperature = 21.0,
            ExteriorTemperature = 14.0,
            RemoteTemperature = 19.5,
            Speed = current.SpeedKmh,
            Heading = 150,
            CurrentJourneyDistance = Math.Round(DemoTrips.InProgressDistanceKm, 1),
            IsLocked = true,
            ClimateOn = false,
            ClimateMode = "off",
            BatteryHeating = false,
            DriverDoorOpen = false,
            PassengerDoorOpen = false,
            RearLeftDoorOpen = false,
            RearRightDoorOpen = false,
            TrunkOpen = false,
            BonnetOpen = false,
            DriverWindowOpen = false,
            PassengerWindowOpen = false,
            RearLeftWindowOpen = false,
            RearRightWindowOpen = false,
            SunRoofOpen = null,
            TyrePressureFrontLeft = 2.4,
            TyrePressureFrontRight = 2.4,
            TyrePressureRearLeft = 2.5,
            // Where the slow leak in the history below has brought it: still above the low line.
            TyrePressureRearRight = 2.26,
            Latitude = current.Lat,
            Longitude = current.Lon,
            Elevation = 3.0,
            BatteryVoltage = 12.7,
            LightsMainBeam = false,
            LightsDippedBeam = false,
            LightsSide = false,
            HeatedSeatFrontLeft = 0,
            HeatedSeatFrontRight = 0,
            RearWindowDefroster = false,
            IsAvailable = true,
            LastVehicleStateAt = DateTime.UtcNow,
            LastChargeStateAt = DateTime.UtcNow.AddHours(-8),
            MileageSinceLastCharge = 32.4,
            MileageOfTheDay = 18.2,
            PowerUsageOfDay = 2.95,
            ChargingScheduleMode = "UNTIL_CONFIGURED_TIME",
            ChargingScheduleStartTime = "00:00",
            ChargingScheduleEndTime = "07:00",
        };
    }

    private static IReadOnlyList<TelemetrySnapshot> BuildHistory()
    {
        var rng = new Random(42);
        var snapshots = new List<TelemetrySnapshot>(HistoryDays * 4);
        var baseDate = DateTime.UtcNow.Date.AddDays(-HistoryDays);
        var odometer = 24300.0;
        var soc = 88.0;
        var fuel = 80.0;
        var usedTodayKwh = 0.0;
        var idCounter = 100L;

        // 4 snapshots per day: depart (7h), midday (12h), return (17h), plug in (22h)
        var dayHours = new[] { 7, 12, 17, 22 };
        for (var day = 0; day < HistoryDays; day++)
        {
            for (var hi = 0; hi < dayHours.Length; hi++)
            {
                var hour = dayHours[hi];
                var ts = baseDate.AddDays(day).AddHours(hour).AddMinutes(rng.Next(0, 30));

                if (hour == 7)
                {
                    // after overnight charge
                    soc = 87 + rng.Next(-2, 8);
                    usedTodayKwh = 0;
                }
                else if (hour == 12)
                {
                    var consumed = 6 + rng.Next(0, 5);
                    soc -= consumed;
                    usedTodayKwh += consumed * 0.7;
                    fuel -= 1.5 + rng.NextDouble();
                    odometer += consumed * 7.2;
                }
                else if (hour == 17)
                {
                    var consumed = 5 + rng.Next(0, 5);
                    soc -= consumed;
                    usedTodayKwh += consumed * 0.7;
                    fuel -= 1.0 + rng.NextDouble();
                    odometer += consumed * 7.2;
                }
                else if (hour == 22 && day % 7 == 0)
                {
                    // weekly refuel
                    fuel = 90 + rng.Next(0, 10);
                }

                soc = Math.Clamp(soc, 10, 97);
                fuel = Math.Clamp(fuel, 5, 100);

                var isCharging = hour == 22;
                // Warm summers and cold winters, so the temperature and range charts have a season.
                var season = Math.Cos(2 * Math.PI * (ts.DayOfYear - 200) / 365.0);
                var extTemp = 11.0 + season * 9.0 + (hour is 12 or 17 ? 4 : 0) + (rng.NextDouble() - 0.5) * 6.0;
                var frontPressure = 2.4 + (rng.NextDouble() - 0.5) * 0.06;
                var rearPressure = 2.5 + (rng.NextDouble() - 0.5) * 0.06;
                // The rear right has a slow puncture: over the last twelve days it loses air the
                // other three keep, for the tyre history and the slow-puncture alert to show.
                var leak = Math.Max(0, day - (HistoryDays - SlowLeakDays)) * SlowLeakBarPerDay;

                snapshots.Add(new TelemetrySnapshot
                {
                    Id = idCounter++,
                    VehicleId = 1,
                    RecordedAt = ts,
                    FuelLevelPercent = Math.Round(fuel, 1),
                    FuelRangeKm = Math.Round(fuel / 100.0 * 650.0, 0),
                    EvSocPercent = Math.Round(soc, 1),
                    HvSocKwh = Math.Round(soc / 100.0 * 70.0, 1),
                    HvTotalCapacityKwh = 70.0,
                    // The counter restarts at midnight. 22:00 UTC is already the next day in much
                    // of Europe, so that reading leaves it out rather than carry the day over.
                    PowerUsageOfDay = hour == 22 ? null : Math.Round(usedTodayKwh, 1),
                    OdometerKm = Math.Round(odometer, 1),
                    IsCharging = isCharging,
                    ChargerConnected = isCharging || hour == 7,
                    ExteriorTemperature = Math.Round(extTemp, 1),
                    InteriorTemperature = Math.Round(extTemp + rng.NextDouble() * 4.0, 1),
                    TyrePressureFrontLeft = Math.Round(frontPressure, 2),
                    TyrePressureFrontRight = Math.Round(frontPressure + (rng.NextDouble() - 0.5) * 0.04, 2),
                    TyrePressureRearLeft = Math.Round(rearPressure, 2),
                    TyrePressureRearRight = Math.Round(rearPressure - leak + (rng.NextDouble() - 0.5) * 0.04, 2),
                    MileageOfTheDay = hour >= 12 ? Math.Round((odometer - 24300 - day * 90) % 200, 1) : 0,
                    ElectricRangeKm = Math.Round(soc / 100.0 * FullBatteryRangeKm * (0.85 + 0.15 * (season + 1) / 2)),
                    // Just back from a drive the car has topped the 12V battery up; parked it rests
                    // lower, and slowly a little lower as the battery ages over the year.
                    BatteryVoltage = Math.Round(hour == 17
                        ? 14.1 + rng.NextDouble() * 0.3
                        : 12.75 - 0.25 * day / HistoryDays + rng.NextDouble() * 0.15 - (hour == 7 ? 0.1 : 0), 2),
                    Latitude = 52.3676 + (rng.NextDouble() - 0.5) * 0.02,
                    Longitude = 4.9041 + (rng.NextDouble() - 0.5) * 0.02,
                    Speed = 0,
                    IsLocked = true,
                    EngineRunning = false,
                    HvBatteryActive = true,
                });
            }
        }

        return snapshots;
    }
}
