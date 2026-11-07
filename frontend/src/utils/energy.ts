import type { TelemetrySnapshot, VehicleType } from '@/services/vehicleApi'

// The gateway publishes powerUsageOfDay and powerUsageSinceLastCharge on the same topics for
// every drivetrain, but they do not measure the same thing on every drivetrain. A car with a
// traction battery reports electrical energy in kWh. A plain hybrid has no such counter, so the
// trip computer puts its fuel total there instead, in hundredths of a litre: read as kWh that
// turns a 59 km day into "394 kWh". Every calculation on those counters goes through this file,
// so which unit applies is decided in one place.
//
// The hundredths-of-a-litre scale is measured, not guessed. Regressing the counter against the
// fuel gauge within each tank over 90 days of an MG HS Hybrid+ (20 tanks, r2 0.94 to 0.997)
// implies a 54.7 L tank against the 55 L the car actually has.

export type EnergyUnit = 'kwh' | 'litres'

/** Hundredths of a litre per litre, the scale a hybrid reports its fuel total in. */
const COUNTER_UNITS_PER_LITRE = 100

/**
 * What the energy counters measure on this drivetrain. Only a plain hybrid reports fuel: a BEV
 * and a PHEV both cycle a traction battery the counter can describe in kWh.
 */
export function energyUnit(vehicleType: VehicleType): EnergyUnit {
  return vehicleType === 'hev' ? 'litres' : 'kwh'
}

export function whPerKm(energyKwh: number | null, distanceKm: number | null): number | null {
  if (energyKwh === null || distanceKm === null || distanceKm <= 0) return null
  return (energyKwh * 1000) / distanceKm
}

/** Litres burned, from a hybrid's fuel counter. */
export function litres(counter: number | null): number | null {
  return counter === null ? null : counter / COUNTER_UNITS_PER_LITRE
}

/** Fuel consumption in L/100 km, from a hybrid's fuel counter over the distance it covers. */
export function litresPer100Km(counter: number | null, distanceKm: number | null): number | null {
  const used = litres(counter)
  if (used === null || distanceKm === null || distanceKm <= 0) return null
  return (used / distanceKm) * 100
}

/**
 * The high-voltage battery as it should be shown, given what the deployment knows about the car.
 *
 * The gateway does not read capacity off the pack: it scales the BMS percentage by an EV-sized
 * default, which is right for a car that has one and off by a factor of forty on a hybrid whose
 * buffer is under 2 kWh (an MG HS Hybrid+ carries 1.83 kWh and is reported as 72.5). So a
 * configured capacity always wins, and a hybrid without one shows percentages only rather than
 * a confident wrong number of kWh.
 */
export interface HvBatteryReading {
  socPercent: number | null
  storedKwh: number | null
  capacityKwh: number | null
}

export function hvBatteryReading(
  status: Pick<TelemetrySnapshot, 'evSocPercent' | 'hvSocKwh' | 'hvTotalCapacityKwh'>,
  vehicleType: VehicleType,
  configuredCapacityKwh: number | null,
): HvBatteryReading {
  const reported = status.hvTotalCapacityKwh
  const capacityKwh = configuredCapacityKwh ?? (vehicleType === 'hev' ? null : reported)

  // drivetrain/soc is the BMS reading itself. The ratio behind it is the fallback for a car that
  // publishes only the derived pair, and survives rescaling because both sides carry the same
  // assumed capacity.
  const socPercent =
    status.evSocPercent ??
    (status.hvSocKwh !== null && reported !== null && reported > 0
      ? (status.hvSocKwh / reported) * 100
      : null)

  return {
    socPercent,
    storedKwh:
      capacityKwh !== null && socPercent !== null ? (capacityKwh * socPercent) / 100 : null,
    capacityKwh,
  }
}

/** What a trip took out of the battery, and where that figure came from. */
export interface TripEnergyFigure {
  kwh: number
  /** Counted by the car itself, or estimated from the state of charge at either end. */
  source: 'car' | 'soc'
  /** Energy per distance over the trip, null when the distance is not known. */
  whPerKm: number | null
}

/**
 * A trip's energy, from what the trip log stores for it. The car's own count wins; without it
 * the drop in state of charge is scaled by the battery's capacity, configured or reported. A
 * plain hybrid's counter holds fuel and its battery is a buffer, so it gets neither.
 */
export function tripEnergy(
  trip: {
    socStartPercent?: number | null
    socEndPercent?: number | null
    energyCounterUsed?: number | null
  },
  distanceKm: number | null,
  vehicleType: VehicleType,
  capacityKwh: number | null,
): TripEnergyFigure | null {
  if (energyUnit(vehicleType) !== 'kwh') return null

  const counted = trip.energyCounterUsed ?? null
  if (counted !== null && counted > 0) {
    return { kwh: counted, source: 'car', whPerKm: whPerKm(counted, distanceKm) }
  }

  const start = trip.socStartPercent ?? null
  const end = trip.socEndPercent ?? null
  if (start === null || end === null || capacityKwh === null || capacityKwh <= 0) return null
  const kwh = ((start - end) / 100) * capacityKwh
  // A level that held or rose says nothing about the trip: the gauge did not move, or it charged.
  if (kwh <= 0) return null
  return { kwh, source: 'soc', whPerKm: whPerKm(kwh, distanceKm) }
}
