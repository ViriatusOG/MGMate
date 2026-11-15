<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import CommandButton from './CommandButton.vue'
import CommandFeedback from './CommandFeedback.vue'
import TimeOfDaySelect from './TimeOfDaySelect.vue'
import RangeControl from './RangeControl.vue'
import { useVehicleCommand } from '@/composables/useVehicleCommand'
import { useCommandDraft } from '@/composables/useCommandDraft'
import { gatewayLabel } from '@/utils/gatewayLabel'
import { timeOfDay } from '@/utils/dates'

// The modes the gateway takes, by the names it reports them under.
const MODES = ['UNTIL_CONFIGURED_TIME', 'UNTIL_CONFIGURED_SOC', 'DISABLED'] as const
type Mode = (typeof MODES)[number]

const COMMAND = 'scheduled-charging'
const TARGET_COMMAND = 'charge-target'
// The car takes 40 to 100 percent in steps of 10.
const TARGET_MIN = 40
const TARGET_MAX = 100
const TARGET_STEP = 10
const DEFAULT_TARGET = 80
const DEFAULT_START = '23:00'
const DEFAULT_END = '07:00'

const props = defineProps<{
  vin: string | null
  mode: string | null
  startTime: string | null
  endTime: string | null
  /** Whether the car takes a charge target, and so can charge until it. */
  canChargeUntilTarget: boolean
  /**
   * The car's charge target, shown as a slider saved along with the schedule. Left out, the form
   * has no slider (the Overview sets the target on the battery card instead).
   */
  target?: number | null
}>()

const withTarget = computed(() => props.canChargeUntilTarget && props.target !== undefined)

const i18n = useI18n()
const { t } = i18n
const { sending, lastResult, isPending, send, waitUntilSettled } = useVehicleCommand()

function targetStep(percent: number | null | undefined): number {
  if (percent == null) return DEFAULT_TARGET
  const stepped = Math.round(percent / TARGET_STEP) * TARGET_STEP
  return Math.min(TARGET_MAX, Math.max(TARGET_MIN, stepped))
}

function isMode(value: string | null): value is Mode {
  return (MODES as readonly (string | null)[]).includes(value)
}

// The schedule the car reports. One it never reported reads as off.
const current = computed(() => ({
  mode: isMode(props.mode) ? props.mode : ('DISABLED' as Mode),
  start: timeOfDay(props.startTime) ?? DEFAULT_START,
  end: timeOfDay(props.endTime) ?? DEFAULT_END,
}))

// The form follows the car, unless a change is on its way to it.
const draft = useCommandDraft(
  () => ({ ...current.value }),
  () => isPending(COMMAND),
)

const modes = computed(() =>
  MODES.filter(
    (mode) =>
      mode !== 'UNTIL_CONFIGURED_SOC' || props.canChargeUntilTarget || current.value.mode === mode,
  ),
)

const targetDraft = useCommandDraft(
  () => targetStep(props.target),
  () => isPending(TARGET_COMMAND),
)

const targetChanged = computed(
  () => withTarget.value && targetDraft.value !== (props.target ?? null),
)

const busy = computed(() => isPending(COMMAND) || isPending(TARGET_COMMAND))

// Only the times a mode uses count: an off schedule has none, charging until the target no end.
const scheduleChanged = computed(() => {
  const d = draft.value
  const c = current.value
  if (d.mode !== c.mode) return true
  if (d.mode === 'DISABLED') return false
  return d.start !== c.start || (d.mode === 'UNTIL_CONFIGURED_TIME' && d.end !== c.end)
})

const changed = computed(() => scheduleChanged.value || targetChanged.value)

// The gateway reads both times whatever the mode, so they always go along. Only what changed is
// sent, and the target waits for the schedule's answer: the car takes one command at a time.
async function save() {
  const { mode, start, end } = draft.value
  const target = targetDraft.value
  const sendTarget = targetChanged.value
  if (scheduleChanged.value) {
    const sent = await send(
      props.vin,
      COMMAND,
      JSON.stringify({ mode, startTime: start, endTime: end }),
      {
        isConfirmed: (s) =>
          s.chargingScheduleMode === mode &&
          (mode === 'DISABLED' || timeOfDay(s.chargingScheduleStartTime) === start) &&
          (mode !== 'UNTIL_CONFIGURED_TIME' || timeOfDay(s.chargingScheduleEndTime) === end),
      },
    )
    if (!sent) return
    if (sendTarget) await waitUntilSettled(COMMAND)
  }
  if (sendTarget) {
    await send(props.vin, TARGET_COMMAND, String(target), {
      isConfirmed: (s) => s.socTargetPercent === target,
    })
  }
}
</script>

<template>
  <div class="detail-modal__section">
    <div class="detail-modal__section-title">{{ t('control.changeSchedule') }}</div>
    <div class="form-stack">
      <div class="form-group">
        <label for="charging-schedule-mode">{{ t('control.chargingScheduleMode') }}</label>
        <select
          id="charging-schedule-mode"
          v-model="draft.mode"
          class="form-select"
          :disabled="isPending(COMMAND)"
        >
          <option v-for="mode in modes" :key="mode" :value="mode">
            {{ gatewayLabel(i18n, 'vehicle.chargingSession.scheduleModes', mode) }}
          </option>
        </select>
      </div>
      <div v-if="draft.mode !== 'DISABLED'" class="form-row">
        <div class="form-group">
          <label for="charging-schedule-start">{{ t('control.scheduleFrom') }}</label>
          <TimeOfDaySelect id="charging-schedule-start" v-model="draft.start" />
        </div>
        <div v-if="draft.mode === 'UNTIL_CONFIGURED_TIME'" class="form-group">
          <label for="charging-schedule-end">{{ t('control.scheduleUntil') }}</label>
          <TimeOfDaySelect id="charging-schedule-end" v-model="draft.end" />
        </div>
      </div>
      <p v-if="draft.mode !== 'DISABLED'" class="text-muted text-xs">
        {{ t('control.scheduleCarTime') }}
      </p>
      <div v-if="withTarget" class="detail-list">
        <RangeControl
          v-model="targetDraft"
          icon="battery-full"
          :label="t('control.chargeTargetLevel')"
          :value-text="`${targetDraft}%`"
          :min="TARGET_MIN"
          :max="TARGET_MAX"
          :step="TARGET_STEP"
          :edge-labels="[`${TARGET_MIN}%`, `${TARGET_MAX}%`]"
          :disabled="isPending(TARGET_COMMAND)"
        />
      </div>
      <CommandButton
        class="btn-primary"
        :pending="busy"
        :sending="sending === COMMAND || sending === TARGET_COMMAND"
        :disabled="!vin || !changed"
        icon="check"
        :label="t('control.saveSchedule')"
        @click="save"
      />
    </div>
    <CommandFeedback :command="COMMAND" :pending="isPending(COMMAND)" :last-result="lastResult" />
    <CommandFeedback
      v-if="withTarget"
      :command="TARGET_COMMAND"
      :pending="isPending(TARGET_COMMAND)"
      :last-result="lastResult"
    />
  </div>
</template>
