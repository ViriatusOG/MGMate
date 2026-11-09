import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { regionIsRightHandDrive } from '@/utils/region'

// What the car reports about its locks, doors, windows, lids, lights and charge cable, shared by
// the MG4 drawing and the list that says the same in words.

export type Corner = 'fl' | 'fr' | 'rl' | 'rr'
export type Tone = 'ok' | 'warn' | 'info' | 'muted'
export interface StatusRow {
  key: string
  label: string
  value: string
  tone: Tone
}

export function useCarStatus() {
  const { t } = useI18n()
  const store = useVehicleStore()
  const uiSettings = useUiSettingsStore()

  const s = computed(() => store.currentStatus)

  // The car reports driver and passenger sides; which of them is on the left depends on where the
  // wheel is, which follows the region (a British car has it on the right).
  const rhd = computed(() => regionIsRightHandDrive(uiSettings.effectiveRegion))

  const doors = computed((): Record<Corner, boolean | null> => {
    const x = s.value
    const driver = x?.driverDoorOpen ?? null
    const passenger = x?.passengerDoorOpen ?? null
    return {
      fl: rhd.value ? passenger : driver,
      fr: rhd.value ? driver : passenger,
      rl: x?.rearLeftDoorOpen ?? null,
      rr: x?.rearRightDoorOpen ?? null,
    }
  })

  const windows = computed((): Record<Corner, boolean | null> => {
    const x = s.value
    const driver = x?.driverWindowOpen ?? null
    const passenger = x?.passengerWindowOpen ?? null
    return {
      fl: rhd.value ? passenger : driver,
      fr: rhd.value ? driver : passenger,
      rl: x?.rearLeftWindowOpen ?? null,
      rr: x?.rearRightWindowOpen ?? null,
    }
  })

  const CORNER_NAME: Record<Corner, string> = {
    fl: 'frontLeft',
    fr: 'frontRight',
    rl: 'rearLeft',
    rr: 'rearRight',
  }

  function cornerLabel(c: Corner): string {
    if (c === 'fl' || c === 'fr') {
      const driverSide = rhd.value ? 'fr' : 'fl'
      return t(c === driverSide ? 'overview.status.driver' : 'overview.status.passenger')
    }
    return t(`overview.status.${CORNER_NAME[c]}`)
  }

  const CORNERS: Corner[] = ['fl', 'fr', 'rl', 'rr']

  function openList(map: Record<Corner, boolean | null>) {
    const open = CORNERS.filter((c) => map[c] === true)
    const known = CORNERS.some((c) => map[c] !== null)
    return { open, known }
  }

  function openStatusRow(
    key: string,
    label: string,
    map: Record<Corner, boolean | null>,
  ): StatusRow | null {
    const { open, known } = openList(map)
    if (!known) return null
    const value =
      open.length === 0
        ? t('overview.status.closed')
        : open.length === 1
          ? `${cornerLabel(open[0]!)} ${t('overview.status.open').toLowerCase()}`
          : t('overview.status.nOpen', { n: open.length })
    return { key, label, value, tone: open.length ? 'warn' : 'ok' }
  }

  function panelStatusRow(
    key: string,
    label: string,
    open: boolean | null | undefined,
  ): StatusRow | null {
    if (open == null) return null
    return {
      key,
      label,
      value: open ? t('overview.status.open') : t('overview.status.closed'),
      tone: open ? 'warn' : 'ok',
    }
  }

  const lightsOn = computed(() => {
    const x = s.value
    if (!x) return null
    const all = [x.lightsMainBeam, x.lightsDippedBeam, x.lightsSide]
    if (all.every((v) => v == null)) return null
    return all.some((v) => v === true)
  })

  const cable = computed((): 'charging' | 'plugged' | 'none' | null => {
    const x = s.value
    if (!x) return null
    if (x.isCharging) return 'charging'
    if (x.chargerConnected) return 'plugged'
    return x.chargerConnected === false ? 'none' : null
  })

  // Compact, the cable row says "Cable" and "Unplugged", so the list fits in two columns.
  function buildRows(compact: boolean): StatusRow[] {
    const x = s.value
    if (!x) return []
    const list: (StatusRow | null)[] = [
      x.isLocked == null
        ? null
        : {
            key: 'locks',
            label: t('overview.status.locks'),
            value: x.isLocked ? t('overview.status.locked') : t('overview.status.unlocked'),
            tone: x.isLocked ? 'ok' : 'warn',
          },
      openStatusRow('doors', t('overview.status.doors'), doors.value),
      openStatusRow('windows', t('overview.status.windows'), windows.value),
      panelStatusRow('boot', t('overview.status.boot'), x.trunkOpen),
      panelStatusRow('bonnet', t('overview.status.bonnet'), x.bonnetOpen),
      panelStatusRow('sunroof', t('overview.status.sunroof'), x.sunRoofOpen),
      lightsOn.value == null
        ? null
        : {
            key: 'lights',
            label: t('overview.status.lights'),
            value: lightsOn.value ? t('overview.status.on') : t('overview.status.off'),
            tone: lightsOn.value ? 'info' : 'muted',
          },
      cable.value == null
        ? null
        : {
            key: 'cable',
            label: t(compact ? 'overview.status.cableShort' : 'overview.status.cable'),
            value:
              cable.value === 'charging'
                ? t('overview.status.cableCharging')
                : cable.value === 'plugged'
                  ? t('overview.status.cablePlugged')
                  : t(compact ? 'overview.status.cableNoneShort' : 'overview.status.cableNone'),
            tone: cable.value === 'none' ? 'muted' : cable.value === 'charging' ? 'ok' : 'info',
          },
    ]
    return list.filter((r): r is StatusRow => r !== null)
  }

  const rows = computed(() => buildRows(false))
  const compactRows = computed(() => buildRows(true))

  const locked = computed(() => s.value?.isLocked ?? null)

  return { status: s, doors, windows, lightsOn, cable, locked, rows, compactRows, cornerLabel }
}
