<script setup lang="ts">
import '@/assets/debug.css'
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import ViewHeader from '@/components/ViewHeader.vue'
import ChipGroup from '@/components/ChipGroup.vue'
import DetailModal from '@/components/DetailModal.vue'
import { useVehicleStore } from '@/stores/vehicle'
import { useUnits } from '@/composables/useUnits'
import { useRelativeTime } from '@/composables/useRelativeTime'
import { useErrorMessage } from '@/composables/useErrorMessage'
import {
  debugApi,
  type DebugEntity,
  type DebugGroup,
  type DebugKept,
  type DebugSnapshot,
} from '@/services/debugApi'
import {
  DEBUG_GROUPS,
  attrList,
  attrNumber,
  attrString,
  iconFor,
  isLocked,
  isOn,
  lockPayloads,
  mainCommand,
  matches,
  quantityFor,
  switchPayloads,
} from '@/utils/debugEntities'

// Everything the MG gateway announces for the car, laid out like a device page in Home Assistant:
// what it reads (sensors, diagnostics) and what it can do (controls, gateway configuration), with
// each value's raw topic and what MGMate keeps of it. Controls ask before they send anything.
const { t } = useI18n()
const vehicleStore = useVehicleStore()
const units = useUnits()
const { relativeTime } = useRelativeTime()
const errorMessage = useErrorMessage()

const REFRESH_MS = 15_000

const snapshot = ref<DebugSnapshot | null>(null)
const loading = ref(false)
const loadError = ref<string | null>(null)
const vin = computed(() => vehicleStore.activeVin)

async function load() {
  if (!vin.value) return
  loading.value = true
  try {
    snapshot.value = await debugApi.get(vin.value)
    loadError.value = null
  } catch (e) {
    loadError.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

let timer: ReturnType<typeof setInterval> | undefined
onMounted(() => {
  void vehicleStore.fetchVehicles()
  timer = setInterval(() => {
    if (document.visibilityState === 'visible') void load()
  }, REFRESH_MS)
})
onBeforeUnmount(() => clearInterval(timer))
watch(vin, () => void load(), { immediate: true })

// ── Filters ──────────────────────────────────────────────────────────────

type Filter = 'all' | 'value' | 'unavailable' | 'notKept'
const filter = ref<Filter>('all')
const search = ref('')
const showHidden = ref(false)

const entities = computed(() => snapshot.value?.entities ?? [])
const hiddenCount = computed(() => entities.value.filter((e) => !e.enabledByDefault).length)

const filterOptions = computed(() =>
  (['all', 'value', 'unavailable', 'notKept'] as const).map((value) => ({
    value,
    label: t(`debug.filters.${value}`),
  })),
)

function passes(e: DebugEntity): boolean {
  if (!showHidden.value && !e.enabledByDefault) return false
  if (!matches(e, search.value)) return false
  switch (filter.value) {
    case 'value':
      return e.rawValue != null
    case 'unavailable':
      return e.rawValue == null
    case 'notKept':
      return e.kept === 'notKept' && e.group !== 'configuration'
    default:
      return true
  }
}

const grouped = computed(() => {
  const out = Object.fromEntries(DEBUG_GROUPS.map((g) => [g, [] as DebugEntity[]])) as Record<
    DebugGroup,
    DebugEntity[]
  >
  for (const e of entities.value) if (passes(e)) out[e.group].push(e)
  return out
})

const withValue = computed(() => entities.value.filter((e) => e.rawValue != null).length)

// ── What MGMate keeps ───────────────────────────────────────────────────

// Per topic rather than per entity: several entities read the same topic.
const coverage = computed(() => {
  const byTopic = new Map<string, DebugKept | 'setting'>()
  for (const e of entities.value) {
    if (!e.topic || !e.kept) continue
    byTopic.set(e.topic, e.group === 'configuration' ? 'setting' : e.kept)
  }
  for (const v of snapshot.value?.otherValues ?? []) byTopic.set(v.topic, v.kept)
  const counts = { history: 0, latest: 0, setting: 0, notKept: 0 }
  const notKept: string[] = []
  for (const [topic, kept] of byTopic) {
    counts[kept]++
    if (kept === 'notKept') notKept.push(topic)
  }
  const total = byTopic.size || 1
  return { counts, notKept: notKept.sort(), total }
})

function keptLabel(e: DebugEntity): { kind: string; text: string } | null {
  if (!e.kept) return null
  if (e.group === 'configuration') return { kind: 'setting', text: t('debug.kept.setting') }
  return { kind: e.kept, text: t(`debug.kept.${e.kept}`) }
}

// Things MG's API cannot do at all, and things the gateway did not announce for this car.
const missing = computed(() => {
  const topics = new Set(entities.value.map((e) => e.topic))
  const items: string[] = []
  if (!topics.has('windows/sunRoof')) items.push(t('debug.missing.sunroof'))
  if (!topics.has('climate/heatedSeatsFrontLeftLevel')) items.push(t('debug.missing.heatedSeats'))
  if (!entities.value.some((e) => e.name === 'Target SoC')) items.push(t('debug.missing.targetSoc'))
  return items
})

// ── Showing a value ─────────────────────────────────────────────────────

function display(e: DebugEntity): string {
  if (e.component === 'climate') {
    const mode = e.states.mode ?? '—'
    const target = e.states.targetTemperature
    return target ? `${mode} · ${target} °C` : mode
  }
  if (e.rawValue == null || e.value == null) return t('debug.unavailable')
  if (e.component === 'device_tracker') {
    try {
      const p = JSON.parse(e.rawValue) as { latitude?: number; longitude?: number }
      if (typeof p.latitude === 'number' && typeof p.longitude === 'number')
        return `${p.latitude.toFixed(4)}, ${p.longitude.toFixed(4)}`
    } catch {
      // Shown as it came.
    }
    return e.rawValue
  }
  if (e.component === 'lock') {
    const locked = isLocked(e)
    return locked == null ? e.value : locked ? t('debug.state.locked') : t('debug.state.unlocked')
  }
  if (e.component === 'binary_sensor' || e.component === 'switch') {
    const on = isOn(e)
    if (on == null) return e.value
    if (e.deviceClass === 'door' || e.deviceClass === 'window' || e.deviceClass === 'opening')
      return on ? t('debug.state.open') : t('debug.state.closed')
    if (e.deviceClass === 'plug')
      return on ? t('debug.state.pluggedIn') : t('debug.state.unplugged')
    return on ? t('debug.state.on') : t('debug.state.off')
  }
  const quantity = quantityFor(e.unit)
  const n = Number(e.value)
  if (quantity && e.value.trim() !== '' && Number.isFinite(n))
    return units.value.format(quantity, n) ?? e.value
  return e.unit ? `${e.value} ${e.unit}` : e.value
}

function age(iso: string | null): string {
  return iso ? relativeTime(iso) : t('debug.beforeStart')
}

// ── Sending ─────────────────────────────────────────────────────────────

interface Pending {
  entity: DebugEntity
  command: string
  value: string
  /** What the person asked for, in words. */
  action: string
}

const pending = ref<Pending | null>(null)
// A choice from a list shows what was picked even when the dialog is cancelled; drawing the lists
// again puts them back on the car's value.
const renderTick = ref(0)

function closePending() {
  pending.value = null
  renderTick.value++
}
const sending = ref(false)
const status = reactive<Record<string, { ok: boolean; text: string }>>({})
const drafts = reactive<Record<string, string>>({})

const canSend = computed(() => snapshot.value?.canSend === true)

function draftKey(e: DebugEntity, command: string) {
  return `${e.id}|${command}`
}

function draft(e: DebugEntity, command: string, fallback: string | null | undefined): string {
  return drafts[draftKey(e, command)] ?? fallback ?? ''
}

function ask(entity: DebugEntity, command: string, value: string, action: string) {
  if (!canSend.value || value === '') return
  pending.value = { entity, command, value, action }
}

function topicFor(p: Pending) {
  return p.entity.commands.find((c) => c.key === p.command)?.topic ?? ''
}

const followUps: ReturnType<typeof setTimeout>[] = []
onBeforeUnmount(() => followUps.forEach(clearTimeout))

async function confirmSend() {
  const p = pending.value
  if (!p || !vin.value) return
  sending.value = true
  try {
    await debugApi.send(vin.value, p.entity.id, p.command, p.value)
    status[p.entity.id] = { ok: true, text: t('debug.sent') }
    delete drafts[draftKey(p.entity, p.command)]
    // The car answers in up to a minute; look again a little later, then once more.
    followUps.push(
      setTimeout(() => void load(), 5_000),
      setTimeout(() => void load(), 30_000),
    )
  } catch (e) {
    status[p.entity.id] = { ok: false, text: errorMessage(e) ?? t('debug.sendFailed') }
  } finally {
    sending.value = false
    closePending()
  }
}

function toggleSwitch(e: DebugEntity) {
  const words = switchPayloads(e)
  const on = isOn(e) === true
  ask(
    e,
    'command_topic',
    on ? words.off : words.on,
    on ? t('debug.action.turnOff') : t('debug.action.turnOn'),
  )
}

function toggleLock(e: DebugEntity) {
  const words = lockPayloads(e)
  const locked = isLocked(e) !== false
  ask(
    e,
    'command_topic',
    locked ? words.unlock : words.lock,
    locked ? t('debug.action.unlock') : t('debug.action.lock'),
  )
}

function commandSupported(e: DebugEntity, key = 'command_topic') {
  return e.commands.find((c) => c.key === key)?.supported === true
}
</script>

<template>
  <div class="debug-page">
    <ViewHeader
      :title="t('debug.title')"
      :subtitle="
        snapshot
          ? t('debug.subtitle', { total: entities.length, withValue })
          : t('debug.subtitleLoading')
      "
    >
      <template #actions>
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="loading"
          @click="load"
        >
          <font-awesome-icon icon="rotate" :spin="loading" />
          {{ t('debug.reload') }}
        </button>
      </template>
    </ViewHeader>

    <p class="debug-banner" role="note">
      <font-awesome-icon icon="triangle-exclamation" aria-hidden="true" />
      {{ t('debug.warning') }}
    </p>

    <p v-if="loadError" class="debug-banner debug-banner--error" role="alert">{{ loadError }}</p>
    <p
      v-else-if="snapshot && !snapshot.connected"
      class="debug-banner debug-banner--error"
      role="alert"
    >
      {{ t('debug.notConnected') }}
    </p>
    <p v-if="snapshot && !snapshot.canSend" class="debug-banner" role="note">
      {{ t('debug.cannotSend') }}
    </p>

    <div class="debug-tools">
      <label class="debug-search">
        <font-awesome-icon icon="magnifying-glass" aria-hidden="true" />
        <span class="debug-sr">{{ t('debug.search') }}</span>
        <input
          id="debug-search"
          v-model="search"
          type="search"
          :placeholder="t('debug.search')"
          autocomplete="off"
        />
      </label>
      <ChipGroup v-model="filter" :options="filterOptions" :group-label="t('debug.filterLabel')" />
      <label v-if="hiddenCount > 0" class="debug-hidden-toggle">
        <input id="debug-show-hidden" v-model="showHidden" type="checkbox" />
        {{ t('debug.showHidden', { n: hiddenCount }) }}
      </label>
    </div>

    <div v-if="snapshot" class="debug-grid">
      <div class="debug-col debug-col--info">
        <section class="debug-card">
          <h2 class="debug-card__title">{{ t('debug.device.title') }}</h2>
          <dl class="debug-info">
            <div>
              <dt>{{ t('debug.device.model') }}</dt>
              <dd>{{ snapshot.device?.model ?? vehicleStore.activeVehicle?.model ?? '—' }}</dd>
            </div>
            <div>
              <dt>{{ t('debug.device.make') }}</dt>
              <dd>{{ snapshot.device?.manufacturer ?? '—' }}</dd>
            </div>
            <div>
              <dt>{{ t('debug.device.hardware') }}</dt>
              <dd>{{ snapshot.device?.hardwareVersion ?? '—' }}</dd>
            </div>
            <div>
              <dt>{{ t('debug.device.gateway') }}</dt>
              <dd>{{ snapshot.device?.gatewayVersion ?? '—' }}</dd>
            </div>
            <div>
              <dt>{{ t('debug.device.broker') }}</dt>
              <dd :class="snapshot.connected ? 'debug-good' : 'text-danger'">
                {{
                  snapshot.connected ? t('debug.device.connected') : t('debug.device.disconnected')
                }}
              </dd>
            </div>
          </dl>
        </section>

        <section class="debug-card">
          <h2 class="debug-card__title">{{ t('debug.coverage.title') }}</h2>
          <div class="debug-coverage__bar" aria-hidden="true">
            <i
              class="debug-kept--history"
              :style="{ width: `${(coverage.counts.history / coverage.total) * 100}%` }"
            />
            <i
              class="debug-kept--latest"
              :style="{ width: `${(coverage.counts.latest / coverage.total) * 100}%` }"
            />
            <i
              class="debug-kept--setting"
              :style="{ width: `${(coverage.counts.setting / coverage.total) * 100}%` }"
            />
            <i
              class="debug-kept--notKept"
              :style="{ width: `${(coverage.counts.notKept / coverage.total) * 100}%` }"
            />
          </div>
          <ul class="debug-coverage__legend">
            <li v-for="kind in ['history', 'latest', 'setting', 'notKept'] as const" :key="kind">
              <span class="debug-dot" :class="`debug-kept--${kind}`" aria-hidden="true" />
              {{ t(`debug.coverage.${kind}`, { n: coverage.counts[kind] }) }}
            </li>
          </ul>
          <p class="debug-card__note">{{ t('debug.coverage.note') }}</p>
          <details v-if="coverage.notKept.length" class="debug-details">
            <summary>{{ t('debug.coverage.notKeptList', { n: coverage.notKept.length }) }}</summary>
            <ul class="debug-topics">
              <li v-for="topic in coverage.notKept" :key="topic">
                <code>{{ topic }}</code>
              </li>
            </ul>
          </details>
        </section>

        <section class="debug-card">
          <h2 class="debug-card__title">{{ t('debug.missing.title') }}</h2>
          <p class="debug-card__note">{{ t('debug.missing.windows') }}</p>
          <ul v-if="missing.length" class="debug-missing">
            <li v-for="item in missing" :key="item">
              <span>{{ item }}</span>
              <span class="debug-pill">{{ t('debug.missing.notAnnounced') }}</span>
            </li>
          </ul>
        </section>
      </div>

      <div
        v-for="column in [
          ['controls', 'configuration'],
          ['sensors', 'diagnostic'],
        ] as const"
        :key="column[0]"
        class="debug-col"
      >
        <template v-for="group in column" :key="group">
          <section v-if="grouped[group].length" class="debug-card">
            <h2 class="debug-card__title">
              {{ t(`debug.groups.${group}`) }}
              <span class="debug-card__count">{{ grouped[group].length }}</span>
            </h2>
            <ul class="debug-list">
              <li
                v-for="e in grouped[group]"
                :key="e.id"
                class="debug-row"
                :class="{ 'debug-row--off': e.rawValue == null && e.component !== 'climate' }"
              >
                <font-awesome-icon :icon="iconFor(e)" class="debug-row__icon" aria-hidden="true" />
                <div class="debug-row__name">
                  <span class="debug-row__label">
                    {{ e.name }}
                    <span v-if="!e.enabledByDefault" class="debug-pill">{{
                      t('debug.hidden')
                    }}</span>
                  </span>
                  <span class="debug-row__meta">
                    <code
                      >{{ e.component }}<template v-if="e.topic"> · {{ e.topic }}</template></code
                    >
                    <span
                      v-if="keptLabel(e)"
                      class="debug-kept"
                      :class="`debug-kept--${keptLabel(e)!.kind}`"
                      >{{ keptLabel(e)!.text }}</span
                    >
                  </span>
                  <span
                    v-if="status[e.id]"
                    class="debug-row__status"
                    :class="status[e.id]!.ok ? 'debug-good' : 'text-danger'"
                    role="status"
                    >{{ status[e.id]!.text }}</span
                  >
                </div>

                <div class="debug-row__value">
                  <!-- Controls -->
                  <template v-if="e.commands.length && mainCommand(e) && !commandSupported(e)">
                    <span class="debug-value" :title="e.rawValue ?? undefined">{{
                      display(e)
                    }}</span>
                    <span class="debug-pill" :title="t('debug.templateOnlyHint')">{{
                      t('debug.templateOnly')
                    }}</span>
                  </template>
                  <template v-else-if="e.component === 'switch'">
                    <button
                      type="button"
                      class="debug-switch"
                      role="switch"
                      :aria-checked="isOn(e) === true"
                      :aria-label="e.name"
                      :disabled="!canSend"
                      @click="toggleSwitch(e)"
                    />
                    <small>{{ display(e) }}</small>
                  </template>
                  <template v-else-if="e.component === 'lock'">
                    <button
                      type="button"
                      class="btn btn-outline-secondary btn-sm"
                      :disabled="!canSend"
                      @click="toggleLock(e)"
                    >
                      <font-awesome-icon :icon="isLocked(e) === false ? 'lock' : 'lock-open'" />
                      {{
                        isLocked(e) === false ? t('debug.action.lock') : t('debug.action.unlock')
                      }}
                    </button>
                    <small>{{ display(e) }}</small>
                  </template>
                  <template v-else-if="e.component === 'select' && mainCommand(e)">
                    <select
                      :id="`debug-${e.id}`"
                      :key="`${e.id}-${renderTick}`"
                      class="form-select form-select-sm debug-input"
                      :value="e.value ?? ''"
                      :aria-label="e.name"
                      :disabled="!canSend"
                      @change="
                        ask(
                          e,
                          'command_topic',
                          ($event.target as HTMLSelectElement).value,
                          t('debug.action.set', {
                            value: ($event.target as HTMLSelectElement).value,
                          }),
                        )
                      "
                    >
                      <option v-if="e.value == null" value="" disabled>—</option>
                      <option v-for="o in attrList(e, 'options')" :key="o" :value="o">
                        {{ o }}
                      </option>
                    </select>
                  </template>
                  <template
                    v-else-if="
                      (e.component === 'number' || e.component === 'text') && mainCommand(e)
                    "
                  >
                    <form
                      class="debug-inline-form"
                      @submit.prevent="
                        ask(
                          e,
                          'command_topic',
                          draft(e, 'command_topic', e.value),
                          t('debug.action.set', { value: draft(e, 'command_topic', e.value) }),
                        )
                      "
                    >
                      <input
                        :id="`debug-${e.id}`"
                        class="form-control form-control-sm debug-input"
                        :type="e.component === 'number' ? 'number' : 'text'"
                        :min="attrNumber(e, 'min') ?? undefined"
                        :max="attrNumber(e, 'max') ?? undefined"
                        :step="attrNumber(e, 'step') ?? undefined"
                        :pattern="attrString(e, 'pattern') ?? undefined"
                        :value="draft(e, 'command_topic', e.value)"
                        :aria-label="e.name"
                        :disabled="!canSend"
                        @input="
                          drafts[draftKey(e, 'command_topic')] = (
                            $event.target as HTMLInputElement
                          ).value
                        "
                      />
                      <span v-if="e.unit" class="debug-unit">{{ e.unit }}</span>
                      <button
                        type="submit"
                        class="btn btn-outline-secondary btn-sm"
                        :disabled="!canSend"
                      >
                        {{ t('debug.action.setShort') }}
                      </button>
                    </form>
                  </template>
                  <template v-else-if="e.component === 'button' && mainCommand(e)">
                    <button
                      type="button"
                      class="btn btn-outline-secondary btn-sm"
                      :disabled="!canSend"
                      @click="
                        ask(
                          e,
                          'command_topic',
                          attrString(e, 'payload_press') ?? 'PRESS',
                          t('debug.action.press'),
                        )
                      "
                    >
                      {{ t('debug.action.press') }}
                    </button>
                  </template>
                  <template v-else-if="e.component === 'climate'">
                    <span class="debug-value">{{ display(e) }}</span>
                    <div class="debug-climate">
                      <select
                        v-if="commandSupported(e, 'mode_command_topic')"
                        :key="`${e.id}-mode-${renderTick}`"
                        :id="`debug-${e.id}-mode`"
                        class="form-select form-select-sm debug-input"
                        :value="e.states.mode ?? ''"
                        :aria-label="`${e.name}: ${t('debug.climateMode')}`"
                        :disabled="!canSend"
                        @change="
                          ask(
                            e,
                            'mode_command_topic',
                            ($event.target as HTMLSelectElement).value,
                            t('debug.action.set', {
                              value: ($event.target as HTMLSelectElement).value,
                            }),
                          )
                        "
                      >
                        <option v-if="!e.states.mode" value="" disabled>—</option>
                        <option v-for="m in attrList(e, 'modes')" :key="m" :value="m">
                          {{ m }}
                        </option>
                      </select>
                      <form
                        v-if="commandSupported(e, 'temperature_command_topic')"
                        class="debug-inline-form"
                        @submit.prevent="
                          ask(
                            e,
                            'temperature_command_topic',
                            draft(e, 'temperature_command_topic', e.states.targetTemperature),
                            t('debug.action.set', {
                              value: `${draft(e, 'temperature_command_topic', e.states.targetTemperature)} °C`,
                            }),
                          )
                        "
                      >
                        <input
                          :id="`debug-${e.id}-temp`"
                          class="form-control form-control-sm debug-input"
                          type="number"
                          :min="attrNumber(e, 'min_temp') ?? undefined"
                          :max="attrNumber(e, 'max_temp') ?? undefined"
                          :value="draft(e, 'temperature_command_topic', e.states.targetTemperature)"
                          :aria-label="`${e.name}: ${t('debug.climateTemperature')}`"
                          :disabled="!canSend"
                          @input="
                            drafts[draftKey(e, 'temperature_command_topic')] = (
                              $event.target as HTMLInputElement
                            ).value
                          "
                        />
                        <span class="debug-unit">°C</span>
                        <button
                          type="submit"
                          class="btn btn-outline-secondary btn-sm"
                          :disabled="!canSend"
                        >
                          {{ t('debug.action.setShort') }}
                        </button>
                      </form>
                    </div>
                  </template>
                  <!-- Read only -->
                  <template v-else>
                    <span
                      class="debug-value"
                      :class="{ 'debug-value--na': e.rawValue == null }"
                      :title="e.rawValue ?? undefined"
                      >{{ display(e) }}</span
                    >
                  </template>
                  <small v-if="e.rawValue != null" class="debug-age">{{ age(e.receivedAt) }}</small>
                </div>
              </li>
            </ul>
          </section>
        </template>
      </div>
    </div>

    <section v-if="snapshot?.otherValues.length" class="debug-card debug-other">
      <details class="debug-details">
        <summary>
          <h2 class="debug-card__title">
            {{ t('debug.other.title') }}
            <span class="debug-card__count">{{ snapshot.otherValues.length }}</span>
          </h2>
        </summary>
        <p class="debug-card__note">{{ t('debug.other.note') }}</p>
        <div class="debug-table-wrap">
          <table class="debug-table">
            <thead>
              <tr>
                <th>{{ t('debug.other.topic') }}</th>
                <th>{{ t('debug.other.value') }}</th>
                <th>{{ t('debug.other.kept') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="v in snapshot.otherValues" :key="v.topic">
                <td>
                  <code>{{ v.topic }}</code>
                </td>
                <td class="debug-table__value">{{ v.value }}</td>
                <td>
                  <span class="debug-kept" :class="`debug-kept--${v.kept}`">{{
                    t(`debug.kept.${v.kept}`)
                  }}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </details>
    </section>

    <DetailModal
      :open="pending != null"
      :title="pending ? `${pending.action}: ${pending.entity.name}` : ''"
      @close="closePending"
    >
      <template v-if="pending">
        <p class="debug-confirm__text">{{ t('debug.confirm.body') }}</p>
        <code class="debug-confirm__topic">{{ topicFor(pending) }}/set ← {{ pending.value }}</code>
      </template>
      <template #footer>
        <button
          type="button"
          class="btn btn-outline-secondary"
          :disabled="sending"
          @click="closePending"
        >
          {{ t('common.cancel') }}
        </button>
        <button type="button" class="btn btn-danger" :disabled="sending" @click="confirmSend">
          {{ t('debug.confirm.send') }}
        </button>
      </template>
    </DetailModal>
  </div>
</template>
