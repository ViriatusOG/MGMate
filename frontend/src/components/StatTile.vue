<script setup lang="ts">
import '@/assets/statTile.css'

// One figure: what it is, the number with its unit, and a line of context under it. Every page
// uses this one tile so figures read the same everywhere; `highlight` marks the page's lead figure.
defineProps<{
  label: string
  value: string | number | null
  unit?: string
  note?: string | null
  /** Colours the note: good news, bad news, or plain context. */
  noteTone?: 'good' | 'bad' | null
  highlight?: boolean
}>()
</script>

<template>
  <div class="stat-tile" :class="{ 'stat-tile--highlight': highlight }">
    <span class="stat-tile__label">{{ label }}</span>
    <span class="stat-tile__value"
      >{{ value ?? '-' }}<small v-if="unit">{{ unit }}</small></span
    >
    <span
      v-if="note || $slots.note"
      class="stat-tile__note"
      :class="noteTone ? `stat-tile__note--${noteTone}` : ''"
      ><slot name="note">{{ note }}</slot></span
    >
  </div>
</template>
