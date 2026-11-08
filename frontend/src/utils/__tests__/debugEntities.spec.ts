import { describe, expect, it } from 'vitest'
import type { DebugEntity } from '@/services/debugApi'
import { iconFor, isLocked, isOn, matches, quantityFor, switchPayloads } from '../debugEntities'

function entity(overrides: Partial<DebugEntity>): DebugEntity {
  return {
    id: 'id',
    component: 'sensor',
    name: 'SoC',
    group: 'sensors',
    enabledByDefault: true,
    deviceClass: null,
    unit: null,
    icon: null,
    topic: 'drivetrain/soc',
    rawValue: '78',
    value: '78',
    receivedAt: null,
    states: {},
    kept: 'history',
    commands: [],
    attributes: {},
    ...overrides,
  }
}

describe('debugEntities', () => {
  it("reads a switch's own on and off words, and Home Assistant's when it names none", () => {
    const front = entity({
      component: 'switch',
      value: 'front',
      attributes: { payload_on: 'front', payload_off: 'off' },
    })
    expect(switchPayloads(front)).toEqual({ on: 'front', off: 'off' })
    expect(isOn(front)).toBe(true)
    expect(switchPayloads(entity({ component: 'switch' }))).toEqual({ on: 'ON', off: 'OFF' })
    expect(isOn(entity({ component: 'switch', value: null }))).toBeNull()
  })

  it('reads a lock by its state words, which the boot names the other way round', () => {
    const doors = {
      payload_lock: 'True',
      payload_unlock: 'False',
      state_locked: 'True',
      state_unlocked: 'False',
    }
    const boot = {
      payload_lock: 'False',
      payload_unlock: 'True',
      state_locked: 'False',
      state_unlocked: 'True',
    }
    expect(isLocked(entity({ component: 'lock', value: 'True', attributes: doors }))).toBe(true)
    expect(isLocked(entity({ component: 'lock', value: 'True', attributes: boot }))).toBe(false)
    expect(isLocked(entity({ component: 'lock', value: 'maybe', attributes: doors }))).toBeNull()
  })

  it("shows distances, pressures and temperatures in the app's units", () => {
    expect(quantityFor('km')).toBe('distance')
    expect(quantityFor('bar')).toBe('pressure')
    expect(quantityFor('°C')).toBe('temperature')
    expect(quantityFor('%')).toBeNull()
  })

  it('finds an entity by its name or its topic', () => {
    const soc = entity({})
    expect(matches(soc, 'soc')).toBe(true)
    expect(matches(soc, 'drivetrain/')).toBe(true)
    expect(matches(soc, 'tyres')).toBe(false)
    expect(matches(soc, '  ')).toBe(true)
  })

  it('picks an icon from the app set', () => {
    expect(iconFor(entity({ component: 'lock' }))).toBe('lock')
    expect(iconFor(entity({ deviceClass: 'door', component: 'binary_sensor' }))).toBe('door-closed')
    expect(iconFor(entity({ topic: 'tyres/frontLeftPressure' }))).toBe('gauge')
    expect(iconFor(entity({}))).toBe('circle-info')
  })
})
