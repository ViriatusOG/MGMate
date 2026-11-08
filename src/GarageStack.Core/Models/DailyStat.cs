namespace GarageStack.Core.Models;

/// <summary>
/// One local day for the statistics page: the averages of what the car reported that day, and the
/// day's total off its energy counter. A value is null when the car reported none of it that day.
/// <c>CounterTotal</c> is in the counter's own unit: kWh out of the traction battery on a plug-in
/// car, hundredths of a litre of fuel on a plain hybrid (see the frontend's utils/energy).
/// The 12V battery and the outside temperature also carry the day's lowest and highest reading:
/// a 12V battery on its way out shows in how low it sags, not in its average. <c>DistanceKm</c>
/// is how far the odometer moved over the readings of that day.
/// </summary>
public sealed record DailyStat(
    DateOnly Date,
    double? EvSocPercent,
    double? FuelLevelPercent,
    double? TyrePressureFrontLeft,
    double? TyrePressureFrontRight,
    double? TyrePressureRearLeft,
    double? TyrePressureRearRight,
    double? BatteryVoltage,
    double? CounterTotal,
    double? BatteryVoltageMin = null,
    double? BatteryVoltageMax = null,
    double? InteriorTemperature = null,
    double? ExteriorTemperature = null,
    double? ExteriorTemperatureMin = null,
    double? ExteriorTemperatureMax = null,
    double? ElectricRangeKm = null,
    double? DistanceKm = null);
