<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { useVehicleStore } from '@/stores/vehicle'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { useCommutePlacesStore } from '@/stores/commutePlaces'
import { chargesApi, type ChargeSession } from '@/services/chargesApi'
import { chargeEnergy, chargeMinutes } from '@/utils/charges'
import { chargeCost, chargePlace, formatMoney } from '@/utils/tariff'
import { formatNumber, formatDateTime } from '@/utils/format'
import { formatDurationMs } from '@/utils/tripRows'

// The newest charge, as its row on the Charges page tells it, so "did it charge last night?" is
// answered without leaving the overview.
const { t } = useI18n()
const store = useVehicleStore()
const uiSettings = useUiSettingsStore()
const places = useCommutePlacesStore()

const charge = ref<ChargeSession | null>(null)
const loaded = ref(false)

const capacityKwh = computed(
  () => store.hvBatteryCapacityKwh ?? store.currentStatus?.hvTotalCapacityKwh ?? null,
)

async function load() {
  const vin = store.activeVin
  if (!vin) return
  if (places.places.length === 0) void places.fetchPlaces(vin)
  try {
    const to = new Date()
    const from = new Date(to.getTime() - 60 * 86_400_000)
    const list = await chargesApi.log(vin, from.toISOString(), to.toISOString())
    charge.value =
      list
        .filter((c) => !c.isHidden)
        .sort((a, b) => Date.parse(b.startedAt) - Date.parse(a.startedAt))[0] ?? null
  } catch {
    charge.value = null
  } finally {
    loaded.value = true
  }
}

onMounted(load)
watch(() => store.activeVin, load)
// A charge that just ended is the one people want to see.
watch(
  () => store.currentStatus?.isCharging,
  (now, before) => {
    if (before && !now) void load()
  },
)

const rows = computed(() => {
  const c = charge.value
  if (!c) return []
  const energy = chargeEnergy(c, capacityKwh.value)
  const place = chargePlace(c, places.places)
  const cost = chargeCost(
    c,
    c.chargerEnergyKwh ?? energy?.kwh ?? null,
    uiSettings.tariff,
    places.places,
  )
  const soc =
    c.socStartPercent !== null && c.socEndPercent !== null
      ? `${Math.round(c.socStartPercent)}% → ${Math.round(c.socEndPercent)}%`
      : null
  const type = [c.chargeType, c.maxPowerKw !== null ? `${formatNumber(c.maxPowerKw, 1)} kW` : null]
    .filter(Boolean)
    .join(' · ')
  return [
    { key: 'battery', label: t('overview.charge.battery'), value: soc },
    {
      key: 'when',
      label: t('overview.charge.when'),
      value: `${formatDateTime(c.startedAt, { weekday: 'short', hour: '2-digit', minute: '2-digit' })} · ${formatDurationMs(chargeMinutes(c) * 60_000, t)}`,
    },
    {
      key: 'where',
      label: t('overview.charge.where'),
      value: [t(`charges.places.${place}`), type].filter(Boolean).join(' · '),
    },
    {
      key: 'wall',
      label: t('overview.charge.fromWall'),
      value:
        c.chargerEnergyKwh != null
          ? `${formatNumber(c.chargerEnergyKwh, 1)} ${t('common.kwh')}`
          : null,
    },
    {
      key: 'cost',
      label: t('overview.charge.cost'),
      value: cost ? formatMoney(cost.amount, uiSettings.tariff?.currency ?? 'GBP') : null,
    },
  ].filter((r) => r.value)
})

const added = computed(() => {
  const c = charge.value
  const kwh = c ? chargeEnergy(c, capacityKwh.value)?.kwh : null
  return kwh == null ? null : `+${formatNumber(kwh, 1)} ${t('common.kwh')}`
})
</script>

<template>
  <section class="ov-card ov-charge" :aria-label="t('overview.charge.title')">
    <header class="ov-card__head">
      <h2 class="ov-card__title">{{ t('overview.charge.title') }}</h2>
      <RouterLink to="/charges" class="ov-card__link">{{ t('overview.charge.all') }}</RouterLink>
    </header>
    <template v-if="charge">
      <div class="ov-charge__added">
        <span class="ov-card__label">{{ t('overview.charge.added') }}</span>
        <strong>{{ added ?? '-' }}</strong>
      </div>
      <dl class="ov-facts">
        <div v-for="r in rows" :key="r.key">
          <dt>{{ r.label }}</dt>
          <dd>{{ r.value }}</dd>
        </div>
      </dl>
    </template>
    <p v-else-if="loaded" class="text-muted text-sm">{{ t('overview.charge.none') }}</p>
  </section>
</template>
