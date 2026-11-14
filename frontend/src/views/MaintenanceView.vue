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
.due-card-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, 20rem), 1fr));
  gap: 0.75rem;
}
</style>
