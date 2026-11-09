<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import DetailModal from './DetailModal.vue'
import { useWarrantyStore } from '@/stores/warranty'
import type { WarrantyCover } from '@/services/warrantyApi'
import { useUnits } from '@/composables/useUnits'
import { useStoreAction } from '@/composables/useStoreAction'
import { useMeasureField } from '@/composables/useMeasureField'
import { ODOMETER_FORMAT } from '@/utils/units'
import { toDateInput } from '@/utils/warranty'

const props = defineProps<{ open: boolean; vin: string }>()
const emit = defineEmits<{ (e: 'close'): void }>()

const { t } = useI18n()
const store = useWarrantyStore()
const units = useUnits()
const { busy, error: actionError, run } = useStoreAction(() => store.actionError)
const unit = computed(() => ({ unit: units.value.symbol('distance') }))

const registrationDate = ref('')
const deliveryDate = ref('')
const deliveryOdometer = useMeasureField(units, 'distance')

// The cover being edited: its id, 'new' for one being added, or null.
const editing = ref<number | 'new' | null>(null)
const name = ref('')
const notes = ref('')
const months = ref<number | '' | null>(null)
const coverDistance = useMeasureField(units, 'distance')
const featured = ref(false)
const validationError = ref<string | null>(null)
const confirmingReset = ref(false)

const covers = computed(() => store.warranty?.covers ?? [])
const formError = computed(() => validationError.value ?? actionError.value)

watch(
  () => props.open,
  (isOpen) => {
    if (!isOpen) return
    const w = store.warranty
    registrationDate.value = toDateInput(w?.registrationDate ?? null)
    deliveryDate.value = toDateInput(w?.deliveryDate ?? null)
    deliveryOdometer.load(w?.deliveryOdometerKm ?? null)
    editing.value = null
    confirmingReset.value = false
    validationError.value = null
    actionError.value = null
  },
)

function close() {
  if (busy.value) return
  emit('close')
}

function summary(c: WarrantyCover): string {
  if (c.months === 0) return t('warranty.notCovered')
  const time = t('warranty.limitMonths', c.months)
  return c.distanceKm != null
    ? `${time} · ${units.value.format('distance', c.distanceKm, ODOMETER_FORMAT)}`
    : time
}

function startEdit(c: WarrantyCover | null) {
  validationError.value = null
  actionError.value = null
  editing.value = c?.id ?? 'new'
  name.value = c?.name ?? ''
  notes.value = c?.notes ?? ''
  months.value = c?.months ?? null
  coverDistance.load(c?.distanceKm ?? null)
  featured.value = c?.featured ?? false
}

async function saveDates() {
  validationError.value = null
  await run(() =>
    store.setDates(props.vin, {
      registrationDate: registrationDate.value || null,
      deliveryDate: deliveryDate.value || null,
      deliveryOdometerKm: deliveryOdometer.metric(),
    }),
  )
}

async function saveCover() {
  validationError.value = null
  if (!name.value.trim()) {
    validationError.value = t('warranty.form.nameRequired')
    return
  }
  if (typeof months.value !== 'number' || !Number.isFinite(months.value)) {
    validationError.value = t('warranty.form.monthsRequired')
    return
  }
  const body = {
    name: name.value.trim(),
    notes: notes.value.trim() || null,
    months: months.value,
    distanceKm: coverDistance.metric(),
    featured: featured.value,
  }
  const id = editing.value
  const saved = await run(() =>
    id === 'new' || id === null
      ? store.addCover(props.vin, body)
      : store.updateCover(props.vin, id, body),
  )
  if (saved) editing.value = null
}

async function deleteCover(id: number) {
  const done = await run(() => store.deleteCover(props.vin, id))
  if (done) editing.value = null
}

async function reset() {
  if (!confirmingReset.value) {
    confirmingReset.value = true
    return
  }
  confirmingReset.value = false
  editing.value = null
  await run(() => store.reset(props.vin))
}
</script>

<template>
  <DetailModal :open="open" :title="t('warranty.edit')" @close="close">
    <form class="form-stack" @submit.prevent="saveDates">
      <div class="form-row">
        <div class="form-group">
          <label for="warranty-registration">{{ t('warranty.form.registrationDate') }}</label>
          <input
            id="warranty-registration"
            v-model="registrationDate"
            type="date"
            class="form-control"
          />
        </div>
        <div class="form-group">
          <label for="warranty-delivery">{{ t('warranty.form.deliveryDate') }}</label>
          <input id="warranty-delivery" v-model="deliveryDate" type="date" class="form-control" />
        </div>
      </div>
      <div class="form-row">
        <div class="form-group">
          <label for="warranty-delivery-odo">{{ t('warranty.form.deliveryOdometer', unit) }}</label>
          <input
            id="warranty-delivery-odo"
            v-model.number="deliveryOdometer.shown"
            type="number"
            min="0"
            class="form-control"
          />
        </div>
        <div class="form-group warranty-edit__save-dates">
          <button type="submit" class="btn btn-primary btn-sm" :disabled="busy">
            {{ t('warranty.form.saveDates') }}
          </button>
        </div>
      </div>
      <p class="text-muted text-xs">{{ t('warranty.form.datesHint') }}</p>
    </form>

    <h4 class="warranty-edit__heading">{{ t('warranty.form.covers') }}</h4>
    <ul class="warranty-edit__list">
      <li v-for="c in covers" :key="c.id">
        <button
          v-if="editing !== c.id"
          type="button"
          class="warranty-edit__row"
          @click="startEdit(c)"
        >
          <span>{{ c.name }}</span>
          <span class="text-muted text-sm">
            {{ summary(c) }} <font-awesome-icon icon="pen-to-square" />
          </span>
        </button>
        <form v-else class="warranty-edit__form form-stack" @submit.prevent="saveCover">
          <div class="form-group">
            <label :for="`warranty-name-${c.id}`">{{ t('warranty.form.name') }}</label>
            <input
              :id="`warranty-name-${c.id}`"
              v-model="name"
              type="text"
              class="form-control"
              maxlength="200"
            />
          </div>
          <div class="form-row">
            <div class="form-group">
              <label :for="`warranty-months-${c.id}`">{{ t('warranty.form.months') }}</label>
              <input
                :id="`warranty-months-${c.id}`"
                v-model.number="months"
                type="number"
                min="0"
                max="360"
                class="form-control"
              />
            </div>
            <div class="form-group">
              <label :for="`warranty-distance-${c.id}`">{{
                t('warranty.form.distance', unit)
              }}</label>
              <input
                :id="`warranty-distance-${c.id}`"
                v-model.number="coverDistance.shown"
                type="number"
                min="1"
                class="form-control"
              />
            </div>
          </div>
          <p class="text-muted text-xs">{{ t('warranty.form.distanceHint') }}</p>
          <div class="form-group">
            <label :for="`warranty-notes-${c.id}`">{{ t('warranty.form.notes') }}</label>
            <input
              :id="`warranty-notes-${c.id}`"
              v-model="notes"
              type="text"
              class="form-control"
              maxlength="500"
            />
          </div>
          <label class="warranty-edit__check">
            <input v-model="featured" type="checkbox" /> {{ t('warranty.form.featured') }}
          </label>
          <div class="warranty-edit__actions">
            <button
              type="button"
              class="btn btn-outline-danger btn-sm"
              :disabled="busy"
              @click="deleteCover(c.id)"
            >
              <font-awesome-icon icon="trash" />{{ t('warranty.form.deleteCover') }}
            </button>
            <span class="warranty-edit__spacer" />
            <button
              type="button"
              class="btn btn-outline-secondary btn-sm"
              :disabled="busy"
              @click="editing = null"
            >
              {{ t('common.cancel') }}
            </button>
            <button type="submit" class="btn btn-primary btn-sm" :disabled="busy">
              {{ t('warranty.form.saveCover') }}
            </button>
          </div>
        </form>
      </li>
      <li v-if="editing === 'new'">
        <form class="warranty-edit__form form-stack" @submit.prevent="saveCover">
          <strong>{{ t('warranty.form.newCover') }}</strong>
          <div class="form-group">
            <label for="warranty-name-new">{{ t('warranty.form.name') }}</label>
            <input
              id="warranty-name-new"
              v-model="name"
              type="text"
              class="form-control"
              maxlength="200"
            />
          </div>
          <div class="form-row">
            <div class="form-group">
              <label for="warranty-months-new">{{ t('warranty.form.months') }}</label>
              <input
                id="warranty-months-new"
                v-model.number="months"
                type="number"
                min="0"
                max="360"
                class="form-control"
              />
            </div>
            <div class="form-group">
              <label for="warranty-distance-new">{{ t('warranty.form.distance', unit) }}</label>
              <input
                id="warranty-distance-new"
                v-model.number="coverDistance.shown"
                type="number"
                min="1"
                class="form-control"
              />
            </div>
          </div>
          <p class="text-muted text-xs">{{ t('warranty.form.distanceHint') }}</p>
          <div class="form-group">
            <label for="warranty-notes-new">{{ t('warranty.form.notes') }}</label>
            <input
              id="warranty-notes-new"
              v-model="notes"
              type="text"
              class="form-control"
              maxlength="500"
            />
          </div>
          <label class="warranty-edit__check">
            <input v-model="featured" type="checkbox" /> {{ t('warranty.form.featured') }}
          </label>
          <div class="warranty-edit__actions">
            <span class="warranty-edit__spacer" />
            <button
              type="button"
              class="btn btn-outline-secondary btn-sm"
              :disabled="busy"
              @click="editing = null"
            >
              {{ t('common.cancel') }}
            </button>
            <button type="submit" class="btn btn-primary btn-sm" :disabled="busy">
              {{ t('warranty.form.saveCover') }}
            </button>
          </div>
        </form>
      </li>
    </ul>

    <p v-if="formError" class="text-danger text-sm" role="alert">{{ formError }}</p>

    <template #footer>
      <button
        class="btn btn-outline-secondary"
        :disabled="busy || editing !== null"
        @click="startEdit(null)"
      >
        <font-awesome-icon icon="plus" />{{ t('warranty.form.addCover') }}
      </button>
      <button class="btn btn-outline-secondary" :disabled="busy" @click="reset">
        <font-awesome-icon icon="rotate-left" />{{
          confirmingReset ? t('warranty.form.resetConfirm') : t('warranty.form.reset')
        }}
      </button>
      <button class="btn btn-primary" :disabled="busy" @click="close">
        {{ t('common.close') }}
      </button>
    </template>
  </DetailModal>
</template>

<style scoped>
.warranty-edit__heading {
  margin: 1rem 0 0.5rem;
  font-size: 0.85rem;
  color: var(--color-text-muted);
}

.warranty-edit__list {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  padding: 0;
  margin: 0;
  list-style: none;
}

.warranty-edit__row {
  display: flex;
  gap: 0.75rem;
  justify-content: space-between;
  width: 100%;
  padding: 0.55rem 0.75rem;
  font: inherit;
  color: inherit;
  text-align: left;
  cursor: pointer;
  background: var(--color-surface-2);
  border: 1px solid var(--color-border);
  border-radius: var(--radius);
}

.warranty-edit__row:hover {
  border-color: var(--color-primary);
}

.warranty-edit__form {
  padding: 0.75rem;
  border: 1px solid var(--color-primary);
  border-radius: var(--radius);
}

.warranty-edit__check {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  font-size: 0.875rem;
}

.warranty-edit__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
}

.warranty-edit__spacer {
  flex: 1;
}

.warranty-edit__save-dates {
  justify-content: flex-end;
  align-self: end;
}
</style>
