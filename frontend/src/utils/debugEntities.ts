import type { DebugEntity, DebugGroup } from '@/services/debugApi'
import type { Quantity } from '@/utils/units'

// How the Debug page reads the gateway's entities: the words a switch or lock sends and shows,
// which of the app's units a value is in, and an icon for each.

export const DEBUG_GROUPS: readonly DebugGroup[] = [
  'controls',
  'sensors',
  'configuration',
  'diagnostic',
]

export function attrString(e: DebugEntity, key: string): string | null {
  const v = e.attributes[key]
  return typeof v === 'string' ? v : null
}

export function attrNumber(e: DebugEntity, key: string): number | null {
  const v = e.attributes[key]
  return typeof v === 'number' && Number.isFinite(v) ? v : null
}

export function attrList(e: DebugEntity, key: string): string[] {
  const v = e.attributes[key]
  return Array.isArray(v) ? v.map(String) : []
}

/** The words a switch sends to turn on and off (Home Assistant's defaults when the config names none). */
export function switchPayloads(e: DebugEntity): { on: string; off: string } {
  return { on: attrString(e, 'payload_on') ?? 'ON', off: attrString(e, 'payload_off') ?? 'OFF' }
}

/** The words a lock sends, and the words its state topic uses. */
export function lockPayloads(e: DebugEntity) {
  return {
    lock: attrString(e, 'payload_lock') ?? 'LOCK',
    unlock: attrString(e, 'payload_unlock') ?? 'UNLOCK',
    locked: attrString(e, 'state_locked') ?? 'LOCKED',
    unlocked: attrString(e, 'state_unlocked') ?? 'UNLOCKED',
  }
}

/** Whether a switch or binary sensor is on; null while it has no value. */
export function isOn(e: DebugEntity): boolean | null {
  if (e.value == null) return null
  return e.value === switchPayloads(e).on
}

/** Whether a lock is locked; null while it has no value or an unknown one. */
export function isLocked(e: DebugEntity): boolean | null {
  const words = lockPayloads(e)
  if (e.value === words.locked) return true
  if (e.value === words.unlocked) return false
  return null
}

/** The command a control sends, the one Home Assistant calls command_topic. */
export function mainCommand(e: DebugEntity) {
  return e.commands.find((c) => c.key === 'command_topic') ?? null
}

/** Which of the app's units a value is in, so it shows in the units chosen in Settings. */
export function quantityFor(unit: string | null): Quantity | null {
  switch (unit) {
    case 'km':
      return 'distance'
    case 'km/h':
      return 'speed'
    case 'bar':
      return 'pressure'
    case '°C':
      return 'temperature'
    default:
      return null
  }
}

const DEVICE_CLASS_ICONS: Record<string, string> = {
  battery: 'battery-three-quarters',
  door: 'door-closed',
  window: 'car-side',
  plug: 'plug',
  power: 'bolt',
  voltage: 'bolt',
  current: 'wave-square',
  energy: 'bolt',
  temperature: 'temperature-half',
  pressure: 'gauge',
  distance: 'road',
  duration: 'clock',
  timestamp: 'clock',
  light: 'lightbulb',
  running: 'car',
}

const COMPONENT_ICONS: Record<string, string> = {
  lock: 'lock',
  switch: 'power-off',
  climate: 'fan',
  number: 'sliders',
  select: 'sliders',
  text: 'pen-to-square',
  button: 'check',
  device_tracker: 'location-dot',
  event: 'message',
}

/** An icon from the app's set for an entity. */
export function iconFor(e: DebugEntity): string {
  if (e.component === 'lock') return 'lock'
  return (
    (e.deviceClass && DEVICE_CLASS_ICONS[e.deviceClass]) ||
    COMPONENT_ICONS[e.component] ||
    (e.topic?.startsWith('tyres/') ? 'gauge' : null) ||
    (e.topic?.startsWith('refresh/') ? 'rotate' : null) ||
    'circle-info'
  )
}

/** Whether a search string matches an entity's name or topic. */
export function matches(e: DebugEntity, query: string): boolean {
  const q = query.trim().toLowerCase()
  if (!q) return true
  return (
    e.name.toLowerCase().includes(q) ||
    (e.topic ?? '').toLowerCase().includes(q) ||
    e.component.includes(q)
  )
}
