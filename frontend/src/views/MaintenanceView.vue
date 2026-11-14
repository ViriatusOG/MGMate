<script setup lang="ts">
import '@/assets/maintenance.css'
import { onMounted, computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useMaintenanceStore } from '@/stores/maintenance'
import { useWarrantyStore } from '@/stores/warranty'
import MaintenanceDueCard from '@/components/MaintenanceDueCard.vue'
import MaintenanceItemFormModal from '@/components/MaintenanceItemFormModal.vue'
import MaintenanceItemDetailModal from '@/components/MaintenanceItemDetailModal.vue'
import type { MaintenanceItem, MaintenanceDueStatus } from '@/services/maintenanceApi'
import { useErrorMessage } from '@/composables/useErrorMessage'
import { useMediaQuery } from '@/composables/useMediaQuery'
import ViewHeader from '@/components/ViewHeader.vue'
import WarrantySection from '@/components/WarrantySection.vue'

const { t } = useI18n()
const vehicleStore = useVehicleStore()
const store = useMaintenanceStore()
const errorMessage = useErrorMessage()

const vin = computed(() => vehicleStore.activeVin)

const formOpen = ref(false)
const detailOpen = ref(false)
const editingItem = ref<MaintenanceItem | null>(null)
const selectedItem = ref<MaintenanceItem | null>(null)

async function load() {
  await vehicleStore.fetchVehicles()
  if (vin.value) await store.fetchItems(vin.value)
}

onMounted(load)

const STATUS_ORDER: Record<MaintenanceDueStatus, number> = {
  overdue: 0,
  dueSoon: 1,
  ok: 2,
  unknown: 3,
}

const sortedItems = computed(() =>
  [...store.items].sort((a, b) => {
    const order = STATUS_ORDER[a.dueStatus] - STATUS_ORDER[b.dueStatus]
    return order !== 0 ? order : a.name.localeCompare(b.name)
  }),
)

// On a phone what is overdue or due soon leads as a short list and keeps its full card; the items
// that are fine take a row each.
const isPhone = useMediaQuery('(width <= 767px)')
const attentionItems = computed(() =>
  sortedItems.value.filter((i) => i.dueStatus === 'overdue' || i.dueStatus === 'dueSoon'),
)
const quietItems = computed(() =>
  sortedItems.value.filter((i) => !attentionItems.value.includes(i)),
)

function openEdit(item: MaintenanceItem) {
  editingItem.value = item
  formOpen.value = true
}

function openAdd() {
  editingItem.value = null
  formOpen.value = true
}

function openDetail(item: MaintenanceItem) {
  selectedItem.value = item
  detailOpen.value = true
}

function openEditFromDetail(item: MaintenanceItem) {
  detailOpen.value = false
  editingItem.value = item
  formOpen.value = true
}

watch(vin, (v) => {
  if (v) store.fetchItems(v)
})

// Items with no service count from the registration date, which the warranty section can change.
const warrantyStore = useWarrantyStore()
watch(
  () => warrantyStore.warranty?.registrationDate,
  (now, before) => {
    if (vin.value && before !== undefined && now !== before) store.fetchItems(vin.value)
  },
)
</script>

<template>
  <div class="view-container">
    <ViewHeader :title="t('maintenance.title')" :subtitle="t('maintenance.subtitle')">
      <template #actions>
        <!-- The form needs a car to add the item to, and only exists once one is known. -->
        <button class="btn btn-primary btn-sm" :disabled="!vin" @click="openAdd">
          <font-awesome-icon icon="plus" />{{ t('maintenance.addItem') }}
        </button>
      </template>
    </ViewHeader>

    <div v-if="store.itemsError" class="empty-state text-danger" role="alert">
      <p>{{ errorMessage(store.itemsError) }}</p>
      <button type="button" class="btn btn-sm btn-outline-secondary" @click="load">
        {{ t('common.retry') }}
      </button>
    </div>
    <div v-else-if="!store.loading && sortedItems.length === 0" class="empty-state">
      {{ t('maintenance.empty') }}
    </div>
    <template v-else-if="isPhone">
      <section v-if="attentionItems.length" class="maint-phone" aria-labelledby="maint-attention">
        <h2 id="maint-attention" class="maint-phone__title">
          {{ t('maintenance.needsAttention') }}
        </h2>
        <ul class="maint-phone__rows">
          <li v-for="item in attentionItems" :key="item.id">
            <MaintenanceDueCard :item="item" compact @open="openDetail(item)" />
          </li>
        </ul>
      </section>
      <section class="maint-phone" aria-labelledby="maint-items">
        <h2 id="maint-items" class="maint-phone__title">{{ t('maintenance.serviceItems') }}</h2>
        <div v-for="item in attentionItems" :key="item.id" class="maint-phone__card">
          <MaintenanceDueCard :item="item" @open="openDetail(item)" />
          <div class="maint-phone__actions">
            <button type="button" class="btn btn-primary btn-sm" @click="openDetail(item)">
              {{ t('maintenance.logService') }}
            </button>
            <button type="button" class="btn btn-outline-secondary btn-sm" @click="openEdit(item)">
              {{ t('maintenance.editItem') }}
            </button>
          </div>
        </div>
        <ul v-if="quietItems.length" class="maint-phone__rows">
          <li v-for="item in quietItems" :key="item.id">
            <MaintenanceDueCard :item="item" compact @open="openDetail(item)" />
          </li>
        </ul>
      </section>
    </template>
    <div v-else class="due-card-list">
      <MaintenanceDueCard
        v-for="item in sortedItems"
        :key="item.id"
        :item="item"
        @open="openDetail(item)"
      />
    </div>

    <WarrantySection v-if="vin" :vin="vin" />

    <MaintenanceItemFormModal
      v-if="vin"
      :open="formOpen"
      :vin="vin"
      :item="editingItem"
      @close="formOpen = false"
    />
    <MaintenanceItemDetailModal
      v-if="vin"
      :open="detailOpen"
      :vin="vin"
      :item="selectedItem"
      @close="detailOpen = false"
      @edit="openEditFromDetail"
    />
  </div>
</template>

<style scoped>
.maint-phone {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  margin-bottom: 1.1rem;
}

.maint-phone__title {
  margin: 0;
  font-size: 1rem;
  font-weight: 700;
}

.maint-phone__rows {
  margin: 0;
  padding: 0;
  overflow: hidden;
  list-style: none;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius);
  box-shadow: var(--shadow-card);
}

.maint-phone__rows li + li {
  border-top: 1px solid var(--color-border);
}

.maint-phone__card {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.maint-phone__actions {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem;
}

.maint-phone__actions .btn {
  justify-content: center;
  min-height: 44px;
}

.due-card-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, 20rem), 1fr));
  gap: 0.75rem;
}
</style>
