<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import StatusBadge from '@/components/StatusBadge.vue'

// One of the schedules the car keeps itself (charging, battery heating): what it is set to, and
// a Change button that opens the form for it underneath.
defineProps<{
  icon: string
  title: string
  /** The big line: the window or start time, or null when the schedule is off. */
  time: string | null
  /** What happens then, under the time. */
  line: string
  on: boolean
  /** Small facts under the line, as label and value. */
  rows?: { label: string; value: string }[]
}>()

const { t } = useI18n()
const editing = ref(false)
</script>

<template>
  <section class="car-schedule" :class="{ 'car-schedule--off': !on }">
    <header class="car-schedule__head">
      <span class="car-schedule__icon" aria-hidden="true"><font-awesome-icon :icon="icon" /></span>
      <div class="car-schedule__heading">
        <h2 class="car-schedule__title">
          {{ title }}
          <StatusBadge :variant="on ? 'success' : 'secondary'">
            {{ on ? t('schedules.on') : t('schedules.off') }}
          </StatusBadge>
        </h2>
        <span class="car-schedule__where">
          <font-awesome-icon icon="car" aria-hidden="true" />{{ t('schedules.savedInCar') }}
        </span>
      </div>
      <button
        type="button"
        class="btn btn-sm btn-outline-secondary car-schedule__change"
        :aria-expanded="editing"
        @click="editing = !editing"
      >
        <font-awesome-icon :icon="editing ? 'xmark' : 'pen-to-square'" />
        <span class="car-schedule__change-text">{{
          editing ? t('schedules.close') : t('schedules.change')
        }}</span>
      </button>
    </header>

    <div class="car-schedule__time">{{ time ?? t('schedules.notSet') }}</div>
    <p class="car-schedule__line">{{ line }}</p>

    <dl v-if="rows?.length" class="car-schedule__rows">
      <div v-for="row in rows" :key="row.label" class="car-schedule__row">
        <dt>{{ row.label }}</dt>
        <dd>{{ row.value }}</dd>
      </div>
    </dl>

    <div v-if="editing" class="car-schedule__form">
      <slot />
    </div>
  </section>
</template>

<style scoped>
.car-schedule {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 1rem 1.1rem;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius);
  min-width: 0;
}

.car-schedule__head {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.35rem;
}

.car-schedule__icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2.25rem;
  height: 2.25rem;
  flex: none;
  border-radius: 8px;
  background: var(--color-surface-2);
  color: var(--color-primary-light);
}

.car-schedule__heading {
  flex: 1;
  min-width: 0;
}

.car-schedule__title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0;
  font-size: 1rem;
  font-weight: 700;
}

.car-schedule__where {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.car-schedule__time {
  font-size: 2rem;
  font-weight: 700;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
}

.car-schedule--off .car-schedule__time {
  color: var(--color-text-muted);
}

.car-schedule__line {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 0.875rem;
}

.car-schedule__rows {
  margin: 0.4rem 0 0;
  border-top: 1px solid var(--color-border);
}

.car-schedule__row {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.5rem 0;
  border-bottom: 1px solid var(--color-border);
  font-size: 0.875rem;
}

.car-schedule__row dt {
  color: var(--color-text-muted);
  font-weight: 400;
}

.car-schedule__row dd {
  margin: 0;
  font-weight: 600;
  text-align: right;
}

.car-schedule__change {
  flex: none;
  min-height: 44px;
}

.car-schedule__form :deep(.detail-modal__section-title) {
  display: none;
}

.car-schedule__form {
  margin-top: 0.75rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--color-border);
}

@media (width <= 767px) {
  .car-schedule__time {
    font-size: 1.75rem;
  }

  /* The icon alone keeps a long title on one line; the button keeps its 44 px target. */
  .car-schedule__change {
    min-width: 44px;
  }

  .car-schedule__change-text {
    position: absolute;
    width: 1px;
    height: 1px;
    overflow: hidden;
    clip-path: inset(50%);
    white-space: nowrap;
  }
}
</style>
