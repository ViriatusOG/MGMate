<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { useVehicleStore } from '@/stores/vehicle'
import { useMaintenanceStore } from '@/stores/maintenance'
import { useTyrePressureThresholds } from '@/composables/useTyrePressureThresholds'
import { useUnits } from '@/composables/useUnits'
import { formatDate, formatNumber } from '@/utils/format'
import { ODOMETER_FORMAT } from '@/utils/units'

// Only what needs doing: a service that is due, a 12V battery running low, a soft tyre. When
// nothing does, the strip is not there at all.
const LOW_12V = 12.0

const { t } = useI18n()
const store = useVehicleStore()
const maintenance = useMaintenanceStore()
const thresholds = useTyrePressureThresholds()
const units = useUnits()

function load() {
  if (store.activeVin) void maintenance.fetchItems(store.activeVin)
}
onMounted(load)
watch(() => store.activeVin, load)

interface Item {
  key: string
  tone: 'danger' | 'warning'
  icon: string
  title: string
  detail: string | null
  to: string
}

// On a phone only the most pressing item shows until the rest is asked for, so the car, its
// range and its controls stay on the first screen.
const props = defineProps<{ collapsed?: boolean }>()
const expanded = ref(false)

const items = computed<Item[]>(() => {
  const out: Item[] = []
  for (const m of maintenance.items) {
    if (m.dueStatus !== 'overdue' && m.dueStatus !== 'dueSoon') continue
    const parts: string[] = []
    if (m.nextDueDate)
      parts.push(
        t('overview.attention.dueOn', {
          date: formatDate(m.nextDueDate, { day: 'numeric', month: 'short' }),
        }),
      )
    if (m.kmRemaining !== null)
      parts.push(
        m.kmRemaining < 0
          ? t('overview.attention.past', {
              distance: units.value.format('distance', -m.kmRemaining, ODOMETER_FORMAT),
            })
          : t('overview.attention.left', {
              distance: units.value.format('distance', m.kmRemaining, ODOMETER_FORMAT),
            }),
      )
    out.push({
      key: `m${m.id}`,
      tone: m.dueStatus === 'overdue' ? 'danger' : 'warning',
      icon: 'screwdriver-wrench',
      title: t(
        m.dueStatus === 'overdue' ? 'overview.attention.overdue' : 'overview.attention.dueSoon',
        { name: m.name },
      ),
      detail: parts.join(' · ') || null,
      to: '/maintenance',
    })
  }
  const s = store.currentStatus
  if (s?.batteryVoltage != null && s.batteryVoltage < LOW_12V) {
    out.push({
      key: '12v',
      tone: 'warning',
      icon: 'car-battery',
      title: t('overview.attention.low12v'),
      detail: `${formatNumber(s.batteryVoltage, 1)} V`,
      to: '/statistics',
    })
  }
  const tyres = [
    ['frontLeft', s?.tyrePressureFrontLeft],
    ['frontRight', s?.tyrePressureFrontRight],
    ['rearLeft', s?.tyrePressureRearLeft],
    ['rearRight', s?.tyrePressureRearRight],
  ] as const
  const soft = tyres.filter(([, bar]) => bar != null && bar < thresholds.value.lowBar)
  if (soft.length > 0) {
    out.push({
      key: 'tyres',
      tone: 'danger',
      icon: 'circle-exclamation',
      title: t('overview.attention.lowTyre', soft.length),
      detail: soft
        .map(
          ([pos, bar]) =>
            `${t(`vehicle.diagram.tyrePosition.${pos}`)} ${units.value.format('pressure', bar!)}`,
        )
        .join(' · '),
      to: '/statistics',
    })
  }
  return out
})

const shown = computed(() =>
  props.collapsed && !expanded.value ? items.value.slice(0, 1) : items.value,
)
const hidden = computed(() => items.value.length - shown.value.length)
</script>

<template>
  <div
    v-if="items.length > 0"
    class="ov-attention"
    role="list"
    :aria-label="t('overview.attention.title')"
  >
    <RouterLink
      v-for="item in shown"
      :key="item.key"
      :to="item.to"
      class="ov-attention__item"
      :class="`ov-attention__item--${item.tone}`"
      role="listitem"
    >
      <span class="ov-attention__icon"><font-awesome-icon :icon="item.icon" /></span>
      <span class="ov-attention__text">
        <b>{{ item.title }}</b>
        <small v-if="item.detail">{{ item.detail }}</small>
      </span>
      <font-awesome-icon icon="chevron-right" class="ov-attention__chev" />
    </RouterLink>
    <button v-if="hidden > 0" type="button" class="ov-attention__more" @click="expanded = true">
      {{ t('overview.attention.more', { n: hidden }, hidden) }}
    </button>
  </div>
</template>
