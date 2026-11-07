<script setup lang="ts">
import '@/assets/logList.css'
import { computed } from 'vue'

// A battery gauge from one level to another: green for what stayed, red for what a trip used or
// amber for what a charge added.
const props = defineProps<{ start: number | null; end: number | null }>()

const shown = computed(() => {
  const { start, end } = props
  if (start === null || end === null) return null
  const lo = Math.max(0, Math.min(start, end))
  const hi = Math.min(100, Math.max(start, end))
  return { lo, hi, charging: end > start, delta: end - start }
})
</script>

<template>
  <span v-if="shown" class="soc-bar" aria-hidden="true">
    <span class="soc-bar__track">
      <span class="soc-bar__kept" :style="{ width: `${shown.lo}%` }" />
      <span
        class="soc-bar__change"
        :class="shown.charging ? 'soc-bar__change--up' : 'soc-bar__change--down'"
        :style="{ left: `${shown.lo}%`, width: `${shown.hi - shown.lo}%` }"
      />
    </span>
  </span>
</template>
