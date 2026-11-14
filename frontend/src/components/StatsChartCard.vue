<script lang="ts">
/** The chart kinds this card can draw, so bar data never has to pose as line data. */
export type StatsChartType = 'line' | 'bar' | 'scatter'
</script>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Chart } from 'vue-chartjs'
import type { ChartData, ChartOptions, Plugin } from 'chart.js'
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  LineController,
  BarElement,
  BarController,
  ScatterController,
  Title,
  Tooltip,
  Legend,
  Filler,
} from 'chart.js'

// Registered once, at module scope, by the only component that renders charts.
ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  LineController,
  BarElement,
  BarController,
  ScatterController,
  Title,
  Tooltip,
  Legend,
  Filler,
)

defineProps<{
  title: string
  icon: string
  type: StatsChartType
  data: ChartData<StatsChartType>
  options: ChartOptions<StatsChartType>
  showInfo: boolean
  /** Plugins for this chart alone, read once when it is made. */
  plugins?: Plugin<StatsChartType>[]
}>()

defineEmits<{ info: [] }>()

const { t } = useI18n()
</script>

<template>
  <div class="chart-container">
    <button
      v-if="showInfo"
      class="card-info-btn"
      :aria-label="t('dashboard.cardInfoBtn')"
      @click.stop="$emit('info')"
    >
      <font-awesome-icon icon="circle-info" />
    </button>
    <h2 class="card-title">
      <font-awesome-icon :icon="icon" />
      {{ title }}
    </h2>
    <!-- Named after its card, so a screen reader says which chart this is instead of nothing. -->
    <Chart
      :type="type"
      :data="data"
      :options="options"
      :plugins="plugins ?? []"
      :aria-label="title"
    />
  </div>
</template>
