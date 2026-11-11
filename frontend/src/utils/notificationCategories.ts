import type { VehicleType } from '@/services/vehicleApi'
import { mayBurnFuel, mayPlugIn } from '@/utils/vehicleType'

export const NOTIFICATION_CATEGORY_IDS = [
  'low-tyre',
  'high-tyre',
  'slow-puncture',
  'low-ev',
  'charging-complete',
  'charging-interrupted',
  'engine-start',
  'unlocked-parked',
  'doors-open-parked',
  'windows-open-parked',
  'moved-parked',
  'vehicle-message',
  'climate-schedule',
  'low-12v',
  'low-fuel',
  'gateway-login',
  'data-stale',
  'place-arrival',
  'place-departure',
  'maintenance',
  'rdw',
] as const

export type NotificationCategoryId = (typeof NOTIFICATION_CATEGORY_IDS)[number]

// Categories the Worker only emits for a car that charges from an external charger: both
// CheckEvSoc and CheckChargingStopped return early unless VehicleTypeHelper.CanCharge, so on a
// plain hybrid these are checklist rows that can never match a notification.
const PLUG_IN_ONLY_CATEGORY_IDS: readonly NotificationCategoryId[] = [
  'low-ev',
  'charging-complete',
  'charging-interrupted',
]

// The Worker's CheckFuelLevel only runs for a car with a combustion engine.
const FUEL_ONLY_CATEGORY_IDS: readonly NotificationCategoryId[] = ['low-fuel']

/**
 * The categories worth offering for a drivetrain. 'unknown' keeps the full list: while detection
 * is still pending, hiding a type the car does support is worse than offering one it doesn't.
 */
export function notificationCategoryIdsFor(
  vehicleType: VehicleType,
): readonly NotificationCategoryId[] {
  return NOTIFICATION_CATEGORY_IDS.filter(
    (id) =>
      (mayPlugIn(vehicleType) || !PLUG_IN_ONLY_CATEGORY_IDS.includes(id)) &&
      (mayBurnFuel(vehicleType) || !FUEL_ONLY_CATEGORY_IDS.includes(id)),
  )
}

// Maintenance notifications carry a dynamic per-item suffix (maintenance-<id>) rather than a
// single fixed string, so they're grouped under one "maintenance" id here. The RDW's carry the
// recall code or the APK date (rdw-recall-<code>, rdw-apk-due-30-<date>) and group as "rdw".
export function notificationCategoryId(category: string | null): NotificationCategoryId | null {
  if (!category) return null
  if (category.startsWith('maintenance-')) return 'maintenance'
  if (category.startsWith('rdw-')) return 'rdw'
  return (NOTIFICATION_CATEGORY_IDS as readonly string[]).includes(category)
    ? (category as NotificationCategoryId)
    : null
}

// The page a notification is about, which tapping it in the panel opens. A category with no
// page of its own (an unknown one, say) opens nothing: the notification just stays put.
const CATEGORY_PAGES: Record<NotificationCategoryId, string> = {
  'low-tyre': '/',
  'high-tyre': '/',
  'slow-puncture': '/',
  'low-ev': '/',
  'charging-complete': '/charges',
  'charging-interrupted': '/charges',
  'engine-start': '/trip-log',
  'unlocked-parked': '/',
  'doors-open-parked': '/',
  'windows-open-parked': '/',
  'moved-parked': '/',
  'vehicle-message': '/',
  'climate-schedule': '/climate-schedules',
  'low-12v': '/',
  'low-fuel': '/',
  'gateway-login': '/',
  'data-stale': '/',
  'place-arrival': '/trip-log',
  'place-departure': '/trip-log',
  maintenance: '/maintenance',
  rdw: '/',
}

export function notificationPage(category: string | null): string | null {
  const id = notificationCategoryId(category)
  return id ? CATEGORY_PAGES[id] : null
}
