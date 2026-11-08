<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'

defineProps<{ moreOpen: boolean }>()
const emit = defineEmits<{ more: [] }>()

const { t } = useI18n()
const route = useRoute()

// The four pages a phone opens most; everything else is one tap away under More, which opens the
// same drawer as the menu button.
const tabs = [
  { to: '/', label: 'nav.tabs.overview', icon: 'gauge-high' },
  { to: '/trip-log', label: 'nav.tabs.trips', icon: 'book' },
  { to: '/charges', label: 'nav.tabs.charges', icon: 'charging-station' },
  { to: '/statistics', label: 'nav.tabs.statistics', icon: 'chart-line' },
]

const onOtherPage = computed(() => !tabs.some((tab) => tab.to === route.path))
</script>

<template>
  <nav class="tab-bar" :aria-label="t('nav.tabsLabel')">
    <RouterLink
      v-for="tab in tabs"
      :key="tab.to"
      :to="tab.to"
      class="tab-bar__tab"
      exact-active-class="is-active"
    >
      <font-awesome-icon :icon="tab.icon" aria-hidden="true" />
      <span>{{ t(tab.label) }}</span>
    </RouterLink>
    <button
      type="button"
      class="tab-bar__tab"
      :class="{ 'is-active': onOtherPage || moreOpen }"
      aria-controls="app-sidebar"
      :aria-expanded="moreOpen"
      @click="emit('more')"
    >
      <font-awesome-icon icon="ellipsis" aria-hidden="true" />
      <span>{{ t('nav.tabs.more') }}</span>
    </button>
  </nav>
</template>
