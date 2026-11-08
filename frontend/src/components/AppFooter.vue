<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useVehicleCommand } from '@/composables/useVehicleCommand'
import { useUpdateNotice } from '@/composables/useUpdateNotice'
import { APP_VERSION } from '@/utils/appVersion'

const { t } = useI18n()
const vehicleStore = useVehicleStore()
const { sending, send } = useVehicleCommand()
const { notice: updateNotice } = useUpdateNotice()

// Joined in script rather than the template, which drops a whitespace-only text node before the version
const copyrightLabel = ['© 2026 MGMate', APP_VERSION].filter(Boolean).join(' ')

const vin = computed(() => vehicleStore.activeVin)

// A forced refresh wakes the car, which costs 12V battery; the gateway already reads MG's cached
// status on its own without waking it, so this asks first.
function refresh() {
  if (!vin.value || !window.confirm(t('control.refreshConfirm'))) return
  send(vin.value, 'refresh', 'force')
}

// On a phone the footer gives its place to the tab bar, and the top bar offers refresh instead.
defineExpose({ refresh })
</script>

<template>
  <footer class="app-footer">
    <div class="app-footer__meta">
      <a
        class="app-footer__copyright"
        href="https://github.com/ViriatusOG/MGMate"
        target="_blank"
        rel="noopener"
        >{{ copyrightLabel }}</a
      >
      <a
        v-if="updateNotice"
        class="app-footer__update"
        :href="updateNotice.releaseUrl"
        target="_blank"
        rel="noopener noreferrer"
        :aria-label="t('updates.availableLabel', { version: updateNotice.version })"
        :title="t('updates.availableLabel', { version: updateNotice.version })"
      >
        <font-awesome-icon icon="circle-up" />
        <span class="app-footer__update-text">{{
          t('updates.available', { version: updateNotice.version })
        }}</span>
      </a>
    </div>
    <div class="app-footer__actions">
      <button
        class="app-footer__btn"
        :aria-label="t('control.refresh')"
        :title="t('control.refreshHint')"
        :disabled="vehicleStore.loading || sending === 'refresh' || !vin"
        @click="refresh"
      >
        <font-awesome-icon icon="rotate" :spin="vehicleStore.loading || sending === 'refresh'" />
        <span class="app-footer__btn-label">{{ t('control.refresh') }}</span>
      </button>
      <RouterLink to="/settings" class="app-footer__btn" :aria-label="t('settings.title')">
        <font-awesome-icon icon="gear" />
        <span class="app-footer__btn-label">{{ t('settings.title') }}</span>
      </RouterLink>
    </div>
  </footer>
</template>
