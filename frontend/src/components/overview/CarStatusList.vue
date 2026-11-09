<script setup lang="ts">
import { computed } from 'vue'
import type { StatusRow } from './useCarStatus'

// The car's status in words. In two columns (the Overview card) the rows fill the left column
// first, then the right one, and drop to one column when the card is too narrow for two.
const props = defineProps<{ rows: StatusRow[]; columns?: 1 | 2 }>()

const style = computed(() =>
  props.columns === 2
    ? { '--ov-status-rows': String(Math.ceil(props.rows.length / 2)) }
    : undefined,
)
</script>

<template>
  <dl class="ov-status__list" :class="{ 'ov-status__list--two': columns === 2 }" :style="style">
    <div v-for="r in rows" :key="r.key">
      <dt>{{ r.label }}</dt>
      <dd :class="`is-${r.tone}`">{{ r.value }}</dd>
    </div>
  </dl>
</template>
