<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import DetailModal from './DetailModal.vue'
import ConfirmFooter from './ConfirmFooter.vue'
import { useMaintenanceStore } from '@/stores/maintenance'
import { useWarrantyStore } from '@/stores/warranty'
import { useVehicleStore } from '@/stores/vehicle'
import type { MaintenanceItem } from '@/services/maintenanceApi'
import { dueStatusVariant, formatIntervalSummary } from '@/utils/maintenance'
import { localDateKey } from '@/utils/dates'
import { formatDate } from '@/utils/format'
import { ODOMETER_FORMAT } from '@/utils/units'
import { useUnits } from '@/composables/useUnits'
import { useMeasureField } from '@/composables/useMeasureField'
import { useStoreAction } from '@/composables/useStoreAction'
import { useTwoTapConfirm } from '@/composables/useTwoTapConfirm'
import StatusBadge from '@/components/StatusBadge.vue'

const props = defineProps<{
  open: boolean
  vin: string
  item: MaintenanceItem | null
}>()

const emit = defineEmits<{ (e: 'close'): void; (e: 'edit', item: MaintenanceItem): void }>()

const { t } = useI18n()
const store = useMaintenanceStore()
const warrantyStore = useWarrantyStore()
const vehicleStore = useVehicleStore()
const units = useUnits()

const performedAt = ref('')
const odometer = useMeasureField(units, 'distance')
const logNotes = ref('')
// Logging a service, removing a log entry and deleting the item each show their own outcome.
const failure = () => store.actionError
const { busy: logSaving, error: logError, run: runLog } = useStoreAction(failure)
const { error: historyError, run: runHistory } = useStoreAction(failure)
const { busy: deleting, error: deleteError, run: runDelete } = useStoreAction(failure)
const pendingDelete = ref(false)

// An item with nothing to count from offers the car's first registration first: MG counts the
// first service from it. A past service is the existing log form.
const countFrom = ref<'registration' | 'pastService'>('registration')
const registrationDate = ref('')
const {
  busy: startSaving,
  error: startError,
  run: runStart,
} = useStoreAction(() => warrantyStore.actionError)
const needsStart = computed(() => currentItem.value?.countingFrom == null)
const showLogForm = computed(() => !needsStart.value || countFrom.value === 'pastService')

async function startFromRegistration() {
  if (!registrationDate.value) return
  // Saving the dates replaces all three, so the delivery the car already has must be known.
  if (!warrantyStore.warranty) await warrantyStore.fetch(props.vin)
  const current = warrantyStore.warranty
  // The page reloads the items when the registration date changes: it moves every item
  // without a service, not only this one.
  await runStart(() =>
    warrantyStore.setDates(props.vin, {
      registrationDate: registrationDate.value,
      deliveryDate: current?.deliveryDate?.slice(0, 10) ?? null,
      deliveryOdometerKm: current?.deliveryOdometerKm ?? null,
    }),
  )
}
// A log entry goes on the second tap: it moves the item's last service back, and there is no undo.
const { armed: armedEntry, tap: tapEntry } = useTwoTapConfirm<number>()

const currentItem = computed(() => store.items.find((i) => i.id === props.item?.id) ?? props.item)
const logEntries = computed(() => (props.item ? (store.logEntries[props.item.id] ?? []) : []))

watch(
  () => props.open,
  (isOpen) => {
    if (!isOpen || !props.item) return
    pendingDelete.value = false
    deleteError.value = null
    logError.value = null
    historyError.value = null
    performedAt.value = localDateKey(new Date())
    odometer.load(vehicleStore.currentStatus?.odometerKm ?? null)
    logNotes.value = ''
    countFrom.value = 'registration'
    startError.value = null
    registrationDate.value = warrantyStore.warranty?.registrationDate?.slice(0, 10) ?? ''
    store.fetchLog(props.vin, props.item.id)
  },
)

async function submitLog() {
  const item = props.item
  if (!item) return
  const logged = await runLog(() =>
    store.logService(props.vin, item.id, {
      performedAt: performedAt.value,
      odometerKm: odometer.metric(),
      notes: logNotes.value.trim() || null,
    }),
  )
  if (logged) logNotes.value = ''
}

async function removeLogEntry(logId: number) {
  const item = props.item
  if (!item || !tapEntry(logId)) return
  await runHistory(() => store.deleteLogEntry(props.vin, item.id, logId))
}

function requestDelete() {
  deleteError.value = null
  pendingDelete.value = true
}

function cancelDelete() {
  pendingDelete.value = false
}

// The modal only closes once the item is really gone; a refused delete stays on screen.
async function confirmDelete() {
  const item = props.item
  if (!item) return
  const deleted = await runDelete(() => store.deleteItem(props.vin, item.id))
  pendingDelete.value = false
  if (deleted) emit('close')
}
</script>

<template>
  <DetailModal
    :open="open && item !== null"
    :title="currentItem?.name ?? ''"
    wide
    @close="emit('close')"
  >
    <div v-if="currentItem">
      <section class="detail-modal__section maintenance-detail">
        <div class="maintenance-detail__summary">
          <StatusBadge :variant="dueStatusVariant(currentItem.dueStatus)">
            {{ t(`maintenance.status.${currentItem.dueStatus}`) }}
          </StatusBadge>
          <span class="text-muted text-sm">{{ formatIntervalSummary(currentItem, t, units) }}</span>
        </div>

        <p v-if="currentItem.nextDueOdometerKm != null" class="text-sm">
          {{ t('maintenance.nextDueOdometer') }}:
          {{ units.format('distance', currentItem.nextDueOdometerKm, ODOMETER_FORMAT) }}
        </p>
        <p v-if="currentItem.nextDueDate" class="text-sm">
          {{ t('maintenance.nextDueDate') }}: {{ formatDate(currentItem.nextDueDate) }}
        </p>
        <p v-if="currentItem.notes" class="text-muted text-sm">{{ currentItem.notes }}</p>
      </section>

      <section v-if="needsStart" class="detail-modal__section">
        <h4 class="detail-modal__section-title">{{ t('maintenance.countFrom.title') }}</h4>
        <div class="maintenance-count-from" role="radiogroup">
          <button
            v-for="option in ['registration', 'pastService'] as const"
            :key="option"
            type="button"
            role="radio"
            :aria-checked="countFrom === option"
            class="btn btn-sm"
            :class="countFrom === option ? 'btn-primary' : 'btn-outline-secondary'"
            @click="countFrom = option"
          >
            {{ t(`maintenance.countFrom.${option}`) }}
          </button>
        </div>
        <form
          v-if="countFrom === 'registration'"
          class="form-stack maintenance-log-form"
          @submit.prevent="startFromRegistration"
        >
          <div class="form-group">
            <label for="count-from-registration">{{
              t('maintenance.countFrom.registrationDate')
            }}</label>
            <input
              id="count-from-registration"
              v-model="registrationDate"
              type="date"
              class="form-control"
              required
            />
          </div>
          <p class="text-muted text-xs">{{ t('maintenance.countFrom.hint') }}</p>
          <p v-if="startError" class="text-danger text-sm" role="alert">{{ startError }}</p>
          <button
            type="submit"
            class="btn btn-primary btn-sm"
            :disabled="startSaving || !registrationDate"
          >
            {{ t('maintenance.countFrom.submit') }}
          </button>
        </form>
      </section>

      <section v-if="showLogForm" class="detail-modal__section">
        <h4 class="detail-modal__section-title">{{ t('maintenance.logService') }}</h4>
        <form class="form-stack maintenance-log-form" @submit.prevent="submitLog">
          <div class="form-row">
            <div class="form-group">
              <label for="log-performed-at">{{ t('maintenance.logForm.performedAt') }}</label>
              <input id="log-performed-at" v-model="performedAt" type="date" class="form-control" />
            </div>
            <div class="form-group">
              <label for="log-odometer">{{
                t('maintenance.logForm.odometer', { unit: units.symbol('distance') })
              }}</label>
              <input
                id="log-odometer"
                v-model.number="odometer.shown"
                type="number"
                min="0"
                class="form-control"
              />
            </div>
          </div>
          <div class="form-group">
            <label for="log-notes">{{ t('maintenance.logForm.notes') }}</label>
            <input
              id="log-notes"
              v-model="logNotes"
              type="text"
              class="form-control"
              maxlength="1000"
            />
          </div>
          <p v-if="logError" class="text-danger text-sm" role="alert">{{ logError }}</p>
          <button type="submit" class="btn btn-primary btn-sm" :disabled="logSaving">
            {{ t('maintenance.logForm.submit') }}
          </button>
        </form>
      </section>

      <section class="detail-modal__section">
        <h4 class="detail-modal__section-title">{{ t('maintenance.history') }}</h4>
        <p v-if="historyError" class="text-danger text-sm" role="alert">{{ historyError }}</p>
        <p v-if="logEntries.length === 0" class="text-muted text-sm">
          {{ t('maintenance.noHistory') }}
        </p>
        <ul v-else class="maintenance-history">
          <li v-for="entry in logEntries" :key="entry.id" class="maintenance-history__row">
            <span>{{ formatDate(entry.performedAt) }}</span>
            <span v-if="entry.odometerKm != null" class="text-muted">
              {{ units.format('distance', entry.odometerKm, ODOMETER_FORMAT) }}
            </span>
            <span v-if="entry.notes" class="text-muted">{{ entry.notes }}</span>
            <button
              type="button"
              class="btn btn-sm maintenance-history__delete"
              :class="armedEntry === entry.id ? 'btn-danger' : 'btn-outline-secondary'"
              :aria-label="
                armedEntry === entry.id
                  ? t('maintenance.confirmDeleteLogEntryLabel')
                  : t('maintenance.deleteLogEntry')
              "
              @click="removeLogEntry(entry.id)"
            >
              <font-awesome-icon
                :icon="armedEntry === entry.id ? 'triangle-exclamation' : 'trash'"
              />
              <template v-if="armedEntry === entry.id">{{
                t('maintenance.confirmDeleteLogEntry')
              }}</template>
            </button>
          </li>
        </ul>
      </section>
    </div>

    <template #footer>
      <ConfirmFooter
        v-if="pendingDelete"
        :question="t('maintenance.deleteConfirm')"
        :busy="deleting"
        @cancel="cancelDelete"
        @confirm="confirmDelete"
      />
      <template v-else>
        <span v-if="deleteError" class="text-danger text-sm" role="alert">{{ deleteError }}</span>
        <button class="btn btn-outline-secondary" @click="requestDelete">
          <font-awesome-icon icon="trash" />{{ t('maintenance.delete') }}
        </button>
        <button v-if="currentItem" class="btn btn-primary" @click="emit('edit', currentItem)">
          <font-awesome-icon icon="pen-to-square" />{{ t('maintenance.editItem') }}
        </button>
      </template>
    </template>
  </DetailModal>
</template>
