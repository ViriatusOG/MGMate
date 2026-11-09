<script setup lang="ts">
import '@/assets/settingsPage.css'
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { useRdwStore } from '@/stores/rdw'
import { useVehicleStore } from '@/stores/vehicle'
import ViewHeader from '@/components/ViewHeader.vue'
import SettingsToggle from '@/components/SettingsToggle.vue'
import RegionalSettings from '@/components/RegionalSettings.vue'
import SideIconSwitch from '@/components/settings/SideIconSwitch.vue'
import CarColorPicker from '@/components/settings/CarColorPicker.vue'
import VehicleTypeSetting from '@/components/settings/VehicleTypeSetting.vue'
import LicensePlateSetting from '@/components/settings/LicensePlateSetting.vue'
import NotificationTypeSettings from '@/components/settings/NotificationTypeSettings.vue'
import ApiTokenSettings from '@/components/settings/ApiTokenSettings.vue'
import HomeLocationSetting from '@/components/settings/HomeLocationSetting.vue'
import { apiUrl } from '@/services/apiCore'

// Every setting on one page, in cards: a section list beside them on a wide screen, one column on
// a phone. Saved to the account as they change, as they were in the dialog this page replaces.
const { t } = useI18n()
const settings = useUiSettingsStore()
const rdw = useRdwStore()
const vehicleStore = useVehicleStore()

// The plate field only exists where the deployment looks plates up, which only the API knows.
watch(
  () => vehicleStore.activeVin,
  (vin) => {
    if (vin) rdw.ensureLoaded(vin)
  },
  { immediate: true },
)

const isLightTheme = computed({
  get: () => settings.theme === 'light',
  set: (val: boolean) => {
    settings.theme = val ? 'light' : 'dark'
  },
})

const sections = computed(() => [
  { id: 'appearance', title: t('settings.theme.title') },
  { id: 'units', title: t('settings.units.title') },
  { id: 'overview', title: t('settings.dashboard.title') },
  { id: 'map', title: t('settings.map.title') },
  { id: 'car', title: t('settings.yourCar') },
  { id: 'home', title: t('settings.home.title') },
  { id: 'notifications', title: t('settings.notificationTypes.title') },
  { id: 'tokens', title: t('settings.apiTokens.title') },
  { id: 'export', title: t('settings.export.title') },
  { id: 'advanced', title: t('settings.advanced.title') },
])

function jumpTo(id: string) {
  document.getElementById(`settings-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}
</script>

<template>
  <div class="settings-page">
    <ViewHeader :title="t('settings.title')" :subtitle="t('settings.pageSubtitle')" />

    <div class="settings-page__layout">
      <nav class="settings-page__toc" :aria-label="t('settings.sectionsLabel')">
        <button
          v-for="s in sections"
          :key="s.id"
          type="button"
          class="settings-page__toc-link"
          @click="jumpTo(s.id)"
        >
          {{ s.title }}
        </button>
      </nav>

      <div class="settings-page__cards">
        <section id="settings-appearance" class="settings-card">
          <h2 class="settings-card__title">{{ t('settings.theme.title') }}</h2>
          <div class="settings-toggles">
            <SideIconSwitch
              v-model="isLightTheme"
              :label="isLightTheme ? t('settings.theme.light') : t('settings.theme.dark')"
              :desc="t('settings.theme.lightDesc')"
              input-id="settings-toggle-theme"
            >
              <template #off><font-awesome-icon icon="moon" /></template>
              <template #on><font-awesome-icon icon="sun" /></template>
            </SideIconSwitch>
          </div>
        </section>

        <section id="settings-units" class="settings-card">
          <h2 class="settings-card__title">{{ t('settings.units.title') }}</h2>
          <RegionalSettings />
        </section>

        <section id="settings-overview" class="settings-card">
          <h2 class="settings-card__title">{{ t('settings.dashboard.title') }}</h2>
          <div class="settings-toggles">
            <SettingsToggle
              v-model="settings.showCardInfoIcons"
              :label="t('settings.dashboard.cardInfoIcons')"
              :desc="t('settings.dashboard.cardInfoIconsDesc')"
              input-id="settings-toggle-card-info-icons"
            />
          </div>
        </section>

        <section id="settings-map" class="settings-card">
          <h2 class="settings-card__title">{{ t('settings.map.title') }}</h2>
          <div class="settings-toggles">
            <SettingsToggle
              v-model="settings.placeNamesEnabled"
              :label="t('settings.map.placeNames')"
              :desc="t('settings.map.placeNamesDesc')"
              input-id="settings-toggle-place-names"
            />
            <SettingsToggle
              v-model="settings.colorfulMaps"
              :label="t('settings.map.colorful')"
              :desc="t('settings.map.colorfulDesc')"
              input-id="settings-toggle-colorful-maps"
            />
          </div>
        </section>

        <section id="settings-car" class="settings-card">
          <h2 class="settings-card__title">{{ t('settings.yourCar') }}</h2>
          <div class="settings-card__group">
            <h3 class="settings-card__subtitle">{{ t('settings.carColor.title') }}</h3>
            <CarColorPicker />
          </div>
          <div class="settings-card__group">
            <h3 class="settings-card__subtitle">{{ t('settings.vehicleType.title') }}</h3>
            <VehicleTypeSetting />
          </div>
          <div v-if="rdw.available" class="settings-card__group">
            <h3 class="settings-card__subtitle">{{ t('settings.licensePlate.title') }}</h3>
            <LicensePlateSetting />
          </div>
        </section>

        <section id="settings-home" class="settings-card">
          <h2 class="settings-card__title">{{ t('settings.home.title') }}</h2>
          <HomeLocationSetting />
        </section>

        <section id="settings-notifications" class="settings-card">
          <h2 class="settings-card__title">{{ t('settings.notificationTypes.title') }}</h2>
          <NotificationTypeSettings />
        </section>

        <section id="settings-tokens" class="settings-card">
          <h2 class="settings-card__title">{{ t('settings.apiTokens.title') }}</h2>
          <ApiTokenSettings />
        </section>

        <section id="settings-export" class="settings-card">
          <h2 class="settings-card__title">{{ t('settings.export.title') }}</h2>
          <div class="form-stack">
            <p class="settings-toggle__desc">{{ t('settings.export.desc') }}</p>
            <a class="btn btn-outline-secondary btn-sm" :href="apiUrl('/api/export')" download>
              <font-awesome-icon icon="download" />
              {{ t('settings.export.download') }}
            </a>
          </div>
        </section>

        <section id="settings-advanced" class="settings-card settings-card--advanced">
          <h2 class="settings-card__title">{{ t('settings.advanced.title') }}</h2>
          <div class="settings-toggles">
            <SettingsToggle
              v-model="settings.showDebugPage"
              :label="t('settings.advanced.showDebug')"
              :desc="t('settings.advanced.showDebugDesc')"
              input-id="settings-toggle-debug-page"
            />
          </div>
          <RouterLink v-if="settings.showDebugPage" to="/debug" class="settings-card__link">
            {{ t('settings.advanced.openDebug') }}
            <font-awesome-icon icon="arrow-right" aria-hidden="true" />
          </RouterLink>
        </section>
      </div>
    </div>
  </div>
</template>
