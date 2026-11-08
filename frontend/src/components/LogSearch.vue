<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { defaultSearch, searchRange, type LogSearchQuery } from '@/utils/logSearch'

// The search card of the trip and charge logs: words and a stretch of days, sent on submit so a
// half-typed word does not fetch a year of entries.
const props = defineProps<{
  /** A search is showing, so the card offers to clear it. */
  active: boolean
  placeholder: string
}>()

const emit = defineEmits<{
  search: [query: LogSearchQuery]
  clear: []
}>()

const { t } = useI18n()
const draft = ref<LogSearchQuery>(defaultSearch())
const problem = ref<string | null>(null)

function submit() {
  const range = searchRange(draft.value)
  if (!range.ok) {
    problem.value = t(`logSearch.invalid.${range.reason}`)
    return
  }
  problem.value = null
  emit('search', { ...draft.value, text: draft.value.text.trim() })
}

function clear() {
  draft.value = defaultSearch()
  problem.value = null
  if (props.active) emit('clear')
}
</script>

<template>
  <form class="ll-card log-search" role="search" @submit.prevent="submit">
    <label class="log-search__text">
      <font-awesome-icon icon="magnifying-glass" class="log-search__icon" />
      <input
        v-model="draft.text"
        type="search"
        class="form-control form-control-sm"
        :placeholder="placeholder"
        :aria-label="t('logSearch.label')"
        enterkeyhint="search"
      />
    </label>
    <div class="log-search__dates">
      <label>
        <span>{{ t('logSearch.from') }}</span>
        <input v-model="draft.from" type="date" class="form-control form-control-sm" required />
      </label>
      <label>
        <span>{{ t('logSearch.to') }}</span>
        <input v-model="draft.to" type="date" class="form-control form-control-sm" required />
      </label>
    </div>
    <p v-if="problem" class="log-search__problem text-xs text-danger" role="alert">
      {{ problem }}
    </p>
    <div class="log-search__actions">
      <button type="submit" class="btn btn-primary btn-sm">
        <font-awesome-icon icon="magnifying-glass" />{{ t('logSearch.search') }}
      </button>
      <button
        v-if="active || draft.text"
        type="button"
        class="btn btn-outline-secondary btn-sm"
        @click="clear"
      >
        {{ t('logSearch.clear') }}
      </button>
    </div>
  </form>
</template>

<style scoped>
.log-search {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}

.log-search__text {
  position: relative;
  display: block;
}

.log-search__icon {
  position: absolute;
  top: 50%;
  left: 0.65rem;
  transform: translateY(-50%);
  color: var(--color-text-muted);
  font-size: 0.8rem;
  pointer-events: none;
}

.log-search__text input {
  padding-left: 2rem;
}

.log-search__dates {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem;
}

.log-search__dates label {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  min-width: 0;
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.log-search__problem {
  margin: 0;
}

.log-search__actions {
  display: flex;
  gap: 0.5rem;
}
</style>
