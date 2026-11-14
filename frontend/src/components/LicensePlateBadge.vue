<script setup lang="ts">
import { computed } from 'vue'
import { formatPlateAs, type PlateStyle } from '@/utils/licensePlate'

// The car's licence plate, small, to sit beside its name: white with the green flash of a British
// zero-emission plate, yellow for a Dutch one, and a plain white plate anywhere else.
const props = defineProps<{
  plate: string
  plateStyle: PlateStyle
  /** A fully electric car, which in Britain carries the green flash on the left. */
  electric?: boolean
}>()

const text = computed(() => formatPlateAs(props.plateStyle, props.plate))
</script>

<template>
  <span
    class="plate-badge"
    :class="[
      `plate-badge--${plateStyle}`,
      { 'plate-badge--flash': plateStyle === 'gb' && electric },
    ]"
    >{{ text }}</span
  >
</template>

<style scoped>
/* The colours of the plate itself, the same in either theme. */
.plate-badge {
  display: inline-block;
  padding: 0.2rem 0.4rem;
  border: 1px solid #8b939c;
  border-radius: 3px;
  background: #fff;
  color: #111;
  font-family: ui-monospace, 'SF Mono', Menlo, Consolas, monospace;
  font-size: 0.75rem;
  font-weight: 700;
  line-height: 1;
  letter-spacing: 0.06em;
  white-space: nowrap;
}

.plate-badge--nl {
  border-color: #111;
  background: #f5c400;
}

.plate-badge--flash {
  padding-left: 0.85rem;
  background: linear-gradient(90deg, #3bb54a 0 0.55rem, #fff 0.55rem);
}
</style>
