import type { ChargeSession } from '@/services/chargesApi'
import type { CommutePlace } from '@/services/commutePlacesApi'
import { distanceKm } from '@/utils/geo'
import { numberLocale } from '@/utils/format'

export type Currency = 'GBP' | 'EUR' | 'USD'
export const CURRENCIES: readonly Currency[] = ['GBP', 'EUR', 'USD']

export type TariffDays = 'all' | 'weekdays' | 'weekends'
export const TARIFF_DAYS: readonly TariffDays[] = ['all', 'weekdays', 'weekends']

/**
 * One price for part of the day: from `from` up to `to` (HH:mm, local time). A band whose end is
 * at or before its start runs past midnight; from equal to to covers the whole day.
 */
export interface TariffBand {
  from: string
  to: string
  /** Per kWh, in the tariff's currency (0.245 for 24.5p). */
  price: number
  days: TariffDays
}

/** What electricity costs: at home, by time of day, and anywhere public when no price is typed in. */
export interface Tariff {
  currency: Currency
  home: TariffBand[]
  publicPrice: number | null
}

export const MAX_TARIFF_BANDS = 12
const MAX_PRICE = 10

export function defaultTariff(currency: Currency = 'GBP'): Tariff {
  return {
    currency,
    home: [{ from: '00:00', to: '00:00', price: 0.245, days: 'all' }],
    publicPrice: 0.79,
  }
}

const TIME = /^([01]\d|2[0-3]):[0-5]\d$/

function validPrice(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 && value <= MAX_PRICE
}

export function isTariffBand(value: unknown): value is TariffBand {
  if (typeof value !== 'object' || value === null) return false
  const band = value as Record<string, unknown>
  return (
    typeof band.from === 'string' &&
    TIME.test(band.from) &&
    typeof band.to === 'string' &&
    TIME.test(band.to) &&
    validPrice(band.price) &&
    (TARIFF_DAYS as readonly unknown[]).includes(band.days)
  )
}

/** A stored tariff, checked as it is read: null when there is none or it cannot be used. */
export function parseTariff(raw: unknown): Tariff | null {
  if (typeof raw !== 'object' || raw === null) return null
  const t = raw as Record<string, unknown>
  if (!(CURRENCIES as readonly unknown[]).includes(t.currency)) return null
  const home = Array.isArray(t.home) ? t.home.filter(isTariffBand).slice(0, MAX_TARIFF_BANDS) : []
  return {
    currency: t.currency as Currency,
    home: home.map((b) => ({ from: b.from, to: b.to, price: b.price, days: b.days })),
    publicPrice: validPrice(t.publicPrice) ? t.publicPrice : null,
  }
}

function minutesOf(hhmm: string): number {
  const [h, m] = hhmm.split(':').map(Number)
  return h! * 60 + m!
}

function bandCovers(band: TariffBand, at: Date): boolean {
  const weekend = at.getDay() === 0 || at.getDay() === 6
  if (band.days === 'weekdays' && weekend) return false
  if (band.days === 'weekends' && !weekend) return false
  const minute = at.getHours() * 60 + at.getMinutes()
  const from = minutesOf(band.from)
  const to = minutesOf(band.to)
  if (from === to) return true
  return from < to ? minute >= from && minute < to : minute >= from || minute < to
}

/** The home price at a moment, local time: the first band that covers it, or null. */
export function homePriceAt(tariff: Tariff, at: Date): number | null {
  return tariff.home.find((b) => bandCovers(b, at))?.price ?? null
}

const SLICE_MS = 5 * 60_000

/**
 * What `kwh` cost at home between two moments, taken as spread evenly over the time: a charge
 * that runs from peak into off-peak pays each rate for its share. Null when a moment has no band.
 */
export function homeCost(
  tariff: Tariff,
  startedAt: string,
  endedAt: string,
  kwh: number,
): number | null {
  const start = Date.parse(startedAt)
  const end = Math.max(start, Date.parse(endedAt))
  const slices = Math.max(1, Math.ceil((end - start) / SLICE_MS))
  const step = (end - start) / slices
  let cost = 0
  for (let i = 0; i < slices; i++) {
    const price = homePriceAt(tariff, new Date(start + step * (i + 0.5)))
    if (price === null) return null
    cost += (price * kwh) / slices
  }
  return cost
}

/** The cheapest home price: what a trip charged at home is reckoned at. */
export function cheapestHomePrice(tariff: Tariff): number | null {
  return tariff.home.length ? Math.min(...tariff.home.map((b) => b.price)) : null
}

export type ChargePlace = 'home' | 'work' | 'public'

/** Where a charge was: as the owner set it, else home when it is at a home place, else public. */
export function chargePlace(
  charge: Pick<ChargeSession, 'place' | 'latitude' | 'longitude'>,
  places: readonly CommutePlace[],
): ChargePlace {
  if (charge.place) return charge.place
  const { latitude, longitude } = charge
  if (latitude === null || longitude === null) return 'public'
  const atHome = places.some(
    (p) =>
      p.kind === 'home' &&
      distanceKm([p.latitude, p.longitude], [latitude, longitude]) * 1000 <= p.radiusMeters,
  )
  return atHome ? 'home' : 'public'
}

export interface ChargeCost {
  amount: number
  /** Typed in by the owner, worked out from the home bands or the public price, or free at work. */
  source: 'paid' | 'home' | 'public' | 'work'
}

/** What a charge cost: the price typed in, else the tariff for where it was. Null when unknown. */
export function chargeCost(
  charge: Pick<
    ChargeSession,
    'paidAmount' | 'place' | 'latitude' | 'longitude' | 'startedAt' | 'endedAt'
  >,
  kwh: number | null,
  tariff: Tariff | null,
  places: readonly CommutePlace[],
): ChargeCost | null {
  if (charge.paidAmount !== null) return { amount: charge.paidAmount, source: 'paid' }
  if (!tariff || kwh === null) return null
  switch (chargePlace(charge, places)) {
    case 'home': {
      const amount = homeCost(tariff, charge.startedAt, charge.endedAt, kwh)
      return amount === null ? null : { amount, source: 'home' }
    }
    case 'work':
      return { amount: 0, source: 'work' }
    case 'public':
      return tariff.publicPrice === null
        ? null
        : { amount: tariff.publicPrice * kwh, source: 'public' }
  }
}

/** An amount of money in the tariff's currency, as the reader's locale writes it. */
export function formatMoney(amount: number, currency: Currency, decimals = 2): string {
  return new Intl.NumberFormat(numberLocale(), {
    style: 'currency',
    currency,
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(amount)
}
