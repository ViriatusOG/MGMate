<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useUpdateNotice } from '@/composables/useUpdateNotice'
import { APP_VERSION } from '@/utils/appVersion'

const { t } = useI18n()
const { notice: updateNotice } = useUpdateNotice()

// Joined in script rather than the template, which drops a whitespace-only text node before the version
const copyrightLabel = ['© 2026 MGMate', APP_VERSION].filter(Boolean).join(' ')
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
  </footer>
</template>
