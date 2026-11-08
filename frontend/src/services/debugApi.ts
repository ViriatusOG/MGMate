import { request, requestJson } from '@/services/apiCore'

/** What MGMate keeps of a value: every poll's copy, only the newest, or nothing. */
export type DebugKept = 'history' | 'latest' | 'notKept'

export type DebugGroup = 'controls' | 'sensors' | 'configuration' | 'diagnostic'

/** One command an entity takes, by its kind in the discovery config (command_topic, mode_command_topic, …). */
export interface DebugCommand {
  key: string
  topic: string
  /** False when its value is built by a template only Home Assistant can fill in. */
  supported: boolean
}

/** One entity the gateway announces for the car. */
export interface DebugEntity {
  id: string
  component: string
  name: string
  group: DebugGroup
  enabledByDefault: boolean
  deviceClass: string | null
  unit: string | null
  icon: string | null
  /** The gateway topic it reads, below saic/…/vehicles/{VIN}/. */
  topic: string | null
  rawValue: string | null
  /** The value as Home Assistant would show it, where its template could be worked out. */
  value: string | null
  /** When the value arrived; null when it predates the API's start. */
  receivedAt: string | null
  /** Further values a climate entity reads: mode and targetTemperature. */
  states: Record<string, string>
  kept: DebugKept | null
  commands: DebugCommand[]
  /** Choices, limits and the on/off words, as the discovery config gives them. */
  attributes: Record<string, unknown>
}

/** A value the gateway publishes that no entity reads. */
export interface DebugValue {
  topic: string
  value: string
  receivedAt: string | null
  kept: DebugKept
}

export interface DebugDevice {
  name: string | null
  manufacturer: string | null
  model: string | null
  hardwareVersion: string | null
  gatewayVersion: string | null
}

export interface DebugSnapshot {
  connected: boolean
  canSend: boolean
  listeningSince: string
  device: DebugDevice | null
  entities: DebugEntity[]
  otherValues: DebugValue[]
}

export const debugApi = {
  get: (vin: string) => request<DebugSnapshot>(`/api/vehicles/${encodeURIComponent(vin)}/debug/`),
  send: (vin: string, entityId: string, command: string, value: string) =>
    requestJson<{ topic: string; payload: string }>(
      `/api/vehicles/${encodeURIComponent(vin)}/debug/commands`,
      'POST',
      { entityId, command, value },
    ),
}
