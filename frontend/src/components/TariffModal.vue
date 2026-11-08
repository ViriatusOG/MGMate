<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import DetailModal from './DetailModal.vue'
import { useUiSettingsStore } from '@/stores/settingsUi'
import {
  CURRENCIES,
  MAX_TARIFF_BANDS,
  TARIFF_DAYS,
  defaultTariff,
  isTariffBand,
  type Currency,
  type TariffBand,
} from '@/utils/tariff'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ (e: 'close'): void }>()

const { t } = useI18n()
const settings = useUiSettingsStore()

// Edited as strings, so a half-typed price does not jump back while the field has focus.
interface BandDraft {
  from: string
  to: string
  price: string
  days: TariffBand['days']
}

const currency = ref<Currency>('GBP')
const bands = ref<BandDraft[]>([])
const publicPrice = ref('')
const invalid = ref(false)

function reset() {
  const tariff = settings.tariff ?? defaultTariff()
  currency.value = tariff.currency
  bands.value = tariff.home.map((b) => ({ ...b, price: String(b.price) }))
  publicPrice.value = tariff.publicPrice === null ? '' : String(tariff.publicPrice)
  invalid.value = false
}

watch(
  () => props.open,
  (open) => open && reset(),
  { immediate: true },
)

function addBand() {
  if (bands.value.length >= MAX_TARIFF_BANDS) return
  bands.value.push({ from: '00:00', to: '00:00', price: '', days: 'all' })
}

function removeBand(index: number) {
  bands.value.splice(index, 1)
}

function readPrice(text: string): number | null {
  const value = Number(text.trim().replace(',', '.'))
  return text.trim() !== '' && Number.isFinite(value) ? value : null
}

function save() {
  const home = bands.value.map((b) => ({ ...b, price: readPrice(b.price) }))
  const pub = publicPrice.value.trim() === '' ? null : readPrice(publicPrice.value)
  if (!home.every(isTariffBand) || (publicPrice.value.trim() !== '' && pub === null)) {
    invalid.value = true
    return
  }
  settings.tariff = { currency: currency.value, home, publicPrice: pub }
  emit('close')
}

function clear() {
  settings.tariff = null
  emit('close')
}
</script>

<template>
  <DetailModal :open="open" :title="t('tariff.title')" @close="emit('close')">
    <form class="tariff" @submit.prevent="save">
      <p class="text-muted text-sm">{{ t('tariff.intro') }}</p>

      <label for="tariff-currency" class="tariff__label">{{ t('tariff.currency') }}</label>
      <select id="tariff-currency" v-model="currency" class="form-select">
        <option v-for="c in CURRENCIES" :key="c" :value="c">{{ c }}</option>
      </select>

      <span class="tariff__label">{{ t('tariff.home') }}</span>
      <p class="text-muted text-xs">{{ t('tariff.bandsHint') }}</p>
      <div v-for="(band, i) in bands" :key="i" class="tariff__band">
        <label>
          <span class="tariff__sub">{{ t('tariff.from') }}</span>
          <input v-model="band.from" type="time" class="form-control" required />
        </label>
        <label>
          <span class="tariff__sub">{{ t('tariff.to') }}</span>
          <input v-model="band.to" type="time" class="form-control" required />
        </label>
        <label>
          <span class="tariff__sub">{{ t('tariff.pricePerKwh', { currency }) }}</span>
          <input v-model="band.price" inputmode="decimal" class="form-control" required />
        </label>
        <label>
          <span class="tariff__sub">{{ t('tariff.days') }}</span>
          <select v-model="band.days" class="form-select">
            <option v-for="d in TARIFF_DAYS" :key="d" :value="d">
              {{ t(`tariff.dayOptions.${d}`) }}
            </option>
          </select>
        </label>
        <button
          type="button"
          class="btn btn-sm btn-outline-secondary tariff__remove"
          :aria-label="t('tariff.removeBand')"
          @click="removeBand(i)"
        >
          <font-awesome-icon icon="xmark" />
        </button>
      </div>
      <button
        v-if="bands.length < MAX_TARIFF_BANDS"
        type="button"
        class="btn btn-sm btn-outline-secondary tariff__add"
        @click="addBand"
      >
        <font-awesome-icon icon="plus" />{{ t('tariff.addBand') }}
      </button>

      <label for="tariff-public" class="tariff__label">{{ t('tariff.public') }}</label>
      <input
        id="tariff-public"
        v-model="publicPrice"
        inputmode="decimal"
        class="form-control"
        :placeholder="t('tariff.publicPlaceholder')"
      />
      <p class="text-muted text-xs">{{ t('tariff.placesHint') }}</p>

      <p v-if="invalid" class="tariff__error" role="alert">{{ t('tariff.invalid') }}</p>

      <div class="tariff__actions">
        <button
          v-if="settings.tariff"
          type="button"
          class="btn btn-outline-secondary"
          @click="clear"
        >
          {{ t('tariff.clear') }}
        </button>
        <button type="submit" class="btn btn-primary">{{ t('tariff.save') }}</button>
      </div>
    </form>
  </DetailModal>
</template>

<style scoped>
.tariff {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.tariff__label {
  margin-top: 0.5rem;
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.tariff__sub {
  display: block;
  font-size: 0.72rem;
  color: var(--color-text-muted);
}

.tariff__band {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr)) auto;
  gap: 0.5rem;
  align-items: end;
}

.tariff__add {
  align-self: flex-start;
}

.tariff__error {
  color: var(--color-danger-text);
}

.tariff__actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

@media (width <= 600px) {
  .tariff__band {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
