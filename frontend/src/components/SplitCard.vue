<script setup lang="ts">
import '@/assets/logList.css'
import { computed } from 'vue'

export interface SplitSlice {
  key: string
  label: string
  count: number
  note: string
  color: string
}

// A split drawn as one thin bar, with each part's count and figures under it. It says what a ring
// chart said in a fifth of the room.
const props = defineProps<{ title: string; slices: SplitSlice[] }>()

const total = computed(() => props.slices.reduce((s, x) => s + x.count, 0))

const parts = computed(() =>
  props.slices
    .filter((s) => s.count > 0)
    .map((s) => ({ key: s.key, color: s.color, share: (s.count / total.value) * 100 })),
)
</script>

<template>
  <section class="split-card">
    <h2 class="split-card__title">{{ title }}</h2>
    <div class="split-card__bar" role="img" :aria-label="title">
      <span
        v-for="p in parts"
        :key="p.key"
        :style="{ width: `${p.share}%`, background: p.color }"
      />
    </div>
    <ul class="split-card__legend">
      <li v-for="s in slices" :key="s.key" :class="{ 'is-empty': s.count === 0 }">
        <span class="split-card__dot" :style="{ background: s.color }" />
        <span
          ><strong>{{ s.count }}</strong> {{ s.label }}</span
        >
        <span class="split-card__note">{{ s.note }}</span>
      </li>
    </ul>
  </section>
</template>
