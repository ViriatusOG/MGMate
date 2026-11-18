<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingsSelect, { type SettingsSelectOption } from '../SettingsSelect.vue'
import { useUiSettingsStore, TYRE_SEASONS, type TyreSeason } from '@/stores/settingsUi'
import {
  useRecommendedTyrePressure,
  useTyrePressureThresholds,
} from '@/composables/useTyrePressureThresholds'
import { useUnits } from '@/composables/useUnits'

// The cold pressures the tyres are set to, a summer and a winter pair, switched by hand. They draw
// the Recommended line on the tyre charts and are what the Android app shows; until set, MG's
// (or the deployment's) pressure is used. Kept with the account's settings, in bar.
const { t } = useI18n()
const settings = useUiSettingsStore()
const thresholds = useTyrePressureThresholds()
const recommended = useRecommendedTyrePressure()
const units = useUnits()

const AXLES = ['front', 'rear'] as const
type Axle = (typeof AXLES)[number]
type Fields = Record<TyreSeason, Record<Axle, string>>

const symbol = computed(() => units.value.symbol('pressure'))
const decimals = computed(() => units.value.decimals('pressure'))
const defaultBar = computed(() => thresholds.value.defaultRecommendedBar)

function shown(bar: number): string {
  return units.value.convert('pressure', bar).toFixed(decimals.value)
}

// What the fields hold when nothing has been typed: the saved pairs, or the default for both.
function savedFields(): Fields {
  const own = settings.tyrePressures
  const pair = (season: TyreSeason) => ({
    front: shown(own ? own[season].frontBar : defaultBar.value),
    rear: shown(own ? own[season].rearBar : defaultBar.value),
  })
  return { summer: pair('summer'), winter: pair('winter') }
}

const fields = reactive<Fields>(savedFields())
const saved = computed(() => JSON.stringify(savedFields()))

// Follows a save from another device, the unit changing, or the default arriving from the server,
// until something is typed here.
watch(saved, (value, previous) => {
  if (JSON.stringify(fields) === previous) Object.assign(fields, JSON.parse(value))
})

function toBar(text: string): number | null {
  const value = Number(text.replace(',', '.'))
  if (!text.trim() || !Number.isFinite(value)) return null
  const bar = units.value.toMetric('pressure', value)
  return bar >= 1 && bar <= 5 ? bar : null
}

const invalid = computed(() =>
  TYRE_SEASONS.flatMap((s) => AXLES.map((a) => toBar(fields[s][a]))).some((b) => b === null),
)
const changed = computed(() => JSON.stringify(fields) !== saved.value)

// A field left as it was keeps its exact value: 2.5 bar shows as 36 psi, which is 2.48 bar.
function barFromField(season: TyreSeason, axle: Axle): number {
  const own = settings.tyrePressures
  const before = own ? own[season][`${axle}Bar`] : defaultBar.value
  return fields[season][axle] === shown(before) ? before : toBar(fields[season][axle])!
}

function pairFromFields(season: TyreSeason) {
  return { frontBar: barFromField(season, 'front'), rearBar: barFromField(season, 'rear') }
}

function save() {
  if (invalid.value) return
  settings.tyrePressures = {
    season: settings.tyrePressures?.season ?? 'summer',
    summer: pairFromFields('summer'),
    winter: pairFromFields('winter'),
  }
}

const seasonOptions = computed((): SettingsSelectOption<TyreSeason>[] =>
  TYRE_SEASONS.map((value) => ({ value, label: t(`settings.tyrePressures.season.${value}`) })),
)

// Switching season keeps both pairs; before any were saved, it saves the default for both.
function chooseSeason(season: TyreSeason) {
  const own = settings.tyrePressures
  const bar = defaultBar.value
  settings.tyrePressures = {
    season,
    summer: own?.summer ?? { frontBar: bar, rearBar: bar },
    winter: own?.winter ?? { frontBar: bar, rearBar: bar },
  }
}

function useDefault() {
  settings.tyrePressures = null
  Object.assign(fields, savedFields())
}

const defaultText = computed(() => units.value.format('pressure', defaultBar.value))
</script>

<template>
  <div class="form-stack">
    <p class="settings-toggle__desc">
      {{ t('settings.tyrePressures.desc', { pressure: defaultText }) }}
    </p>

    <div class="settings-toggles">
      <SettingsSelect
        id="settings-tyre-season"
        :model-value="recommended.season ?? 'summer'"
        :label="t('settings.tyrePressures.season.label')"
        :desc="t('settings.tyrePressures.season.desc')"
        :options="seasonOptions"
        @update:model-value="chooseSeason"
      />
    </div>

    <form class="tyre-pressure-grid" @submit.prevent="save">
      <span aria-hidden="true"></span>
      <span v-for="axle in AXLES" :key="axle" class="tyre-pressure-grid__head">
        {{ t(`settings.tyrePressures.${axle}`) }}
      </span>
      <template v-for="season in TYRE_SEASONS" :key="season">
        <span class="settings-toggle__label">
          {{ t(`settings.tyrePressures.season.${season}`) }}
          <span v-if="recommended.season === season" class="badge badge-info">
            {{ t('settings.tyrePressures.inUse') }}
          </span>
        </span>
        <label v-for="axle in AXLES" :key="axle" class="tyre-pressure-grid__field">
          <input
            v-model="fields[season][axle]"
            class="form-control form-control-sm"
            type="text"
            inputmode="decimal"
            :aria-label="
              t('settings.tyrePressures.fieldLabel', {
                season: t(`settings.tyrePressures.season.${season}`),
                axle: t(`settings.tyrePressures.${axle}`),
              })
            "
          />
          <span class="text-muted text-sm">{{ symbol }}</span>
        </label>
      </template>
      <div class="tyre-pressure-grid__actions">
        <button type="submit" class="btn btn-primary btn-sm" :disabled="invalid || !changed">
          {{ t('settings.tyrePressures.save') }}
        </button>
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="settings.tyrePressures === null"
          @click="useDefault"
        >
          {{ t('settings.tyrePressures.useDefault', { pressure: defaultText }) }}
        </button>
      </div>
    </form>
    <p v-if="invalid" class="text-danger text-sm" role="alert">
      {{ t('settings.tyrePressures.invalid') }}
    </p>
  </div>
</template>
