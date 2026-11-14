<script setup lang="ts">
// A page's title row: the heading, the page's actions on the right, an optional caption under the
// heading inside the row (the period Statistics shows, or what a page puts in its meta slot, as
// the Overview does with the car's name and plate), and an optional line about the page under the
// row.
defineProps<{
  title: string
  caption?: string
  subtitle?: string
}>()
</script>

<template>
  <div class="view-header">
    <div v-if="caption || $slots.meta">
      <h1>{{ title }}</h1>
      <p v-if="caption" class="text-muted text-sm view-header__caption">{{ caption }}</p>
      <div v-if="$slots.meta" class="view-header__meta"><slot name="meta" /></div>
    </div>
    <h1 v-else>{{ title }}</h1>
    <div v-if="$slots.actions" class="view-header__actions">
      <slot name="actions" />
    </div>
  </div>
  <p v-if="subtitle" class="text-muted view-subtitle">{{ subtitle }}</p>
</template>
