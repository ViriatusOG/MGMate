<script setup lang="ts">
// The period's figures as a phone shows them: the distance as the one big number, up to three
// figures about the driving under it, and the rest as a short list.
export interface StatsSummaryFigure {
  id: string
  icon: string
  label: string
  value: string | null
  unit?: string
  clickable?: boolean
}

defineProps<{
  /** The headline figure; left out when the user has hidden it. */
  hero: StatsSummaryFigure | null
  /** What stands under the headline, such as the number of trips. */
  note: string | null
  tiles: readonly StatsSummaryFigure[]
  rows: readonly StatsSummaryFigure[]
}>()

const emit = defineEmits<{ (e: 'select', id: string): void }>()
</script>

<template>
  <div class="stats-phone">
    <div v-if="hero || tiles.length > 0" class="stats-phone__card">
      <template v-if="hero">
        <span class="stats-phone__label">{{ hero.label }}</span>
        <span class="stats-phone__hero">
          {{ hero.value ?? '-'
          }}<small v-if="hero.unit && hero.value !== null">{{ hero.unit }}</small>
        </span>
        <span v-if="note" class="stats-phone__note">{{ note }}</span>
      </template>
      <dl v-if="tiles.length > 0" class="stats-phone__tiles">
        <div v-for="tile in tiles" :key="tile.id" class="stats-phone__tile">
          <dt>{{ tile.label }}</dt>
          <dd>
            {{ tile.value ?? '-'
            }}<small v-if="tile.unit && tile.value !== null">{{ tile.unit }}</small>
          </dd>
        </div>
      </dl>
    </div>

    <ul v-if="rows.length > 0" class="stats-phone__rows">
      <li v-for="row in rows" :key="row.id">
        <component
          :is="row.clickable ? 'button' : 'div'"
          class="stats-phone__row"
          :type="row.clickable ? 'button' : undefined"
          @click="row.clickable && emit('select', row.id)"
        >
          <span class="stats-phone__icon"><font-awesome-icon :icon="row.icon" /></span>
          <span class="stats-phone__row-label">{{ row.label }}</span>
          <span class="stats-phone__row-value">
            {{ row.value ?? '-'
            }}<template v-if="row.unit && row.value !== null"> {{ row.unit }}</template>
          </span>
          <font-awesome-icon
            v-if="row.clickable"
            icon="chevron-right"
            class="stats-phone__chevron"
            aria-hidden="true"
          />
        </component>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.stats-phone {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.stats-phone__card,
.stats-phone__rows {
  border: 1px solid var(--color-border);
  border-radius: var(--radius);
  background: var(--color-surface);
  box-shadow: var(--shadow-card);
}

.stats-phone__card {
  display: flex;
  flex-direction: column;
  padding: 1rem;
}

.stats-phone__label {
  font-size: 0.72rem;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.stats-phone__hero {
  font-size: 2.6rem;
  font-weight: 800;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
}

.stats-phone__hero small {
  margin-left: 0.2rem;
  font-size: 1.1rem;
  font-weight: 500;
  color: var(--color-text-muted);
}

.stats-phone__note {
  margin-top: 0.15rem;
  font-size: 0.85rem;
  color: var(--color-text-muted);
}

.stats-phone__tiles {
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: minmax(0, 1fr);
  gap: 0.5rem;
  margin: 0.85rem 0 0;
}

.stats-phone__card > .stats-phone__tiles:first-child {
  margin-top: 0;
}

.stats-phone__tile {
  min-width: 0;
  padding: 0.55rem 0.65rem;
  border-radius: 10px;
  background: var(--color-surface-2);
}

.stats-phone__tile dt {
  overflow: hidden;
  font-size: 0.65rem;
  font-weight: 600;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--color-text-muted);
}

.stats-phone__tile dd {
  margin: 0.1rem 0 0;
  font-size: 1.15rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.stats-phone__tile dd small {
  margin-left: 0.15rem;
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--color-text-muted);
}

.stats-phone__rows {
  margin: 0;
  padding: 0;
  list-style: none;
  overflow: hidden;
}

.stats-phone__rows li + li {
  border-top: 1px solid var(--color-border);
}

.stats-phone__row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  width: 100%;
  min-height: 48px;
  padding: 0.55rem 0.9rem;
  border: 0;
  background: none;
  color: inherit;
  font: inherit;
  text-align: left;
}

button.stats-phone__row {
  cursor: pointer;
}

.stats-phone__icon {
  display: inline-grid;
  flex: none;
  place-items: center;
  width: 2rem;
  height: 2rem;
  border-radius: 8px;
  background: var(--color-surface-2);
  color: var(--color-text-muted);
}

.stats-phone__row-label {
  flex: 1;
  min-width: 0;
  font-weight: 600;
}

.stats-phone__row-value {
  flex: none;
  color: var(--color-text-muted);
  font-variant-numeric: tabular-nums;
}

.stats-phone__chevron {
  flex: none;
  font-size: 0.75rem;
  color: var(--color-text-muted);
}
</style>
