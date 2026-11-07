import { computed, ref, type Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ChartData, ChartOptions } from 'chart.js'
import type { StatsChartType } from '@/components/StatsChartCard.vue'
import type { TripLogEntry } from '@/services/tripLogApi'
import type { DailyStat } from '@/services/vehicleApi'
import type { StatsChartId } from '@/stores/settingsShared'
import type { VehicleType } from '@/stores/vehicle'
import { energyUnit, litres } from '@/utils/energy'
import { formatDate, numberLocale } from '@/utils/format'
import {
  bucketAverages,
  bucketMaxes,
  bucketMins,
  bucketTickFormat,
  bucketTotals,
  consumptionByTemperature,
  linearFit,
  roundedRange,
  roundTo2,
  type StatsBucket,
} from '@/utils/statistics'
import type { StatsGrouping } from '@/utils/statsPeriod'
import { burnsFuel, mayPlugIn, plugsIn } from '@/utils/vehicleType'
import { useChartTheme } from './useChartTheme'
import { useUnits } from './useUnits'

export interface ChartDef {
  id: StatsChartId
  icon: string
  title: string
  description: string
  /** Whether this chart means anything for the drivetrain at all. */
  vehicleApplicable: boolean
  /** Whether there is something to draw right now. */
  applicable: boolean
  type: StatsChartType
  data: ChartData<StatsChartType>
  options: ChartOptions<StatsChartType>
}

const CHART_ICONS: Record<StatsChartId, string> = {
  evChart: 'bolt',
  voltage12vChart: 'car-battery',
  tyreChart: 'gauge',
  hybridSocChart: 'wave-square',
  dailyKwhChart: 'bolt-lightning',
  distanceChart: 'route',
  rangeChart: 'road',
  temperatureChart: 'temperature-half',
  consumptionTempChart: 'snowflake',
}

// A day's lowest and highest reading, drawn thin and dashed around its average.
function extremeDataset(label: string, data: (number | null)[], color: string) {
  return {
    ...lineDataset(label, data, color),
    borderWidth: 1.5,
    borderDash: [5, 4],
    pointRadius: 0,
  }
}

const hasAny = (values: readonly (number | null)[]) => values.some((v) => v !== null)

/** An axis that fits the values with a little room, in round steps; Chart.js picks one when there are none. */
function fittedRange(values: readonly (number | null)[], minSpan: number) {
  const valid = values.filter((v): v is number => v !== null)
  if (!valid.length) return {}
  const low = Math.min(...valid)
  const high = Math.max(...valid)
  const pad = Math.max(minSpan - (high - low), 0) / 2
  return roundedRange(low - pad, high + pad)
}

// Named in the legend by their short translated position (FL in English, LV in Dutch).
const TYRE_SERIES: {
  position: 'frontLeft' | 'frontRight' | 'rearLeft' | 'rearRight'
  read: (d: DailyStat) => number | null
  color: string
}[] = [
  { position: 'frontLeft', read: (p) => p.tyrePressureFrontLeft, color: '#f59e0b' },
  { position: 'frontRight', read: (p) => p.tyrePressureFrontRight, color: '#ef4444' },
  { position: 'rearLeft', read: (p) => p.tyrePressureRearLeft, color: '#8b5cf6' },
  { position: 'rearRight', read: (p) => p.tyrePressureRearRight, color: '#ec4899' },
]

/** A level the car reports in percent: the fuel tank's, or the traction battery's charge. */
export type LevelKind = 'fuel' | 'battery'

// Each level's line, in the colours every chart draws it in, filled where it is the only line.
const LEVEL_SERIES: Record<
  LevelKind,
  {
    labelKey: string
    icon: string
    read: (d: DailyStat) => number | null
    color: string
    fill: string
  }
> = {
  battery: {
    labelKey: 'vehicle.evSoc',
    icon: 'bolt',
    read: (d) => d.evSocPercent,
    color: '#10b981',
    fill: 'rgba(16,185,129,0.1)',
  },
  fuel: {
    labelKey: 'vehicle.fuel',
    icon: 'gas-pump',
    read: (d) => d.fuelLevelPercent,
    color: '#3b82f6',
    fill: 'rgba(59,130,246,0.1)',
  },
}

// A level's axis runs the whole way, so a quarter tank looks like one.
const PERCENT_AXIS = { min: 0, max: 100 }

// Every line series shares the same shape and styling; only label, data and colour differ.
function lineDataset(label: string, data: (number | null)[], color: string, fillColor?: string) {
  return {
    label,
    data,
    borderColor: color,
    backgroundColor: fillColor ?? 'transparent',
    fill: fillColor !== undefined,
    tension: 0.3,
    spanGaps: true,
    pointRadius: 2,
    pointHoverRadius: 4,
  }
}

/**
 * What every chart of a period's points shares: each point's name, in full for the tooltip and
 * briefly for the axis, the axes, legend and theme colours, and the line a level draws.
 */
function useChartFrame(buckets: Ref<StatsBucket[]>, grouping: Ref<StatsGrouping>) {
  const { t } = useI18n()
  const chartTheme = useChartTheme()

  // The tooltip names a point in full: the day, the week it starts, or the month.
  const labels = computed(() =>
    buckets.value.map((b) => {
      if (grouping.value === 'month') return formatDate(b.start, { month: 'long', year: 'numeric' })
      const day = formatDate(b.start)
      return grouping.value === 'week' ? t('statistics.weekOf', { date: day }) : day
    }),
  )
  // The axis names each point briefly, since a month of full dates crowds a phone's chart.
  const tickLabels = computed(() => {
    const options = bucketTickFormat(buckets.value, grouping.value)
    return buckets.value.map((b) => formatDate(b.start, options))
  })

  // All charts share the same responsive/axis setup; they differ only in aspect ratio, legend
  // and y-axis range. The locale makes axis and tooltip numbers read like the rest of the page,
  // and the theme's colours make the axes, grid and legend fit it.
  function chartOptions(aspectRatio: number, legend: boolean, y: { min?: number; max?: number }) {
    const { text, grid } = chartTheme.value
    const axis = { grid: { color: grid }, border: { color: grid } }
    return {
      locale: numberLocale(),
      responsive: true,
      maintainAspectRatio: true,
      aspectRatio,
      animation: false as const,
      // Short swatches leave a phone room for more legend entries on one line.
      plugins: { legend: { display: legend, labels: { color: text, boxWidth: 20 } } },
      scales: {
        x: {
          ...axis,
          ticks: {
            color: text,
            maxRotation: 0,
            autoSkip: true,
            maxTicksLimit: 8,
            // On a category axis a tick's value is the index of its label.
            callback: (value: string | number) => tickLabels.value[Number(value)] ?? value,
          },
        },
        y: { ...y, ...axis, ticks: { color: text } },
      },
    }
  }

  function levelDataset(level: LevelKind, filled: boolean) {
    const series = LEVEL_SERIES[level]
    return lineDataset(
      `${t(series.labelKey)} (%)`,
      bucketAverages(buckets.value, series.read),
      series.color,
      filled ? series.fill : undefined,
    )
  }

  return { labels, chartOptions, levelDataset }
}

/**
 * The statistics view's charts, one point per day, week or month of the period: the traction
 * battery, the tyres, a hybrid's battery against its tank, and the energy (or fuel) used. Levels
 * average over a point's days; energy adds up. Each carries whether it applies to this drivetrain
 * and whether there is anything to draw yet.
 */
export function useStatisticsCharts(
  buckets: Ref<StatsBucket[]>,
  grouping: Ref<StatsGrouping>,
  vehicleType: Ref<VehicleType>,
  /** The period's trips with their consumption, for the chart against outside temperature. */
  trips: Ref<readonly TripLogEntry[]> = ref([]),
  capacityKwh: Ref<number | null> = ref(null),
): Ref<ChartDef[]> {
  const { t } = useI18n()
  const units = useUnits()
  const chartTheme = useChartTheme()
  const { labels, chartOptions, levelDataset } = useChartFrame(buckets, grouping)

  const hasLargeEv = computed(() => mayPlugIn(vehicleType.value))
  const isHybrid = computed(() => burnsFuel(vehicleType.value))
  // Every known drivetrain counts its daily energy: a plain hybrid in litres of fuel, a plug-in
  // in kWh. Only an undetected car is left out, since its counter could be either.
  const countsDailyEnergy = computed(
    () => burnsFuel(vehicleType.value) || plugsIn(vehicleType.value),
  )
  const hasData = computed(() => buckets.value.length > 0)

  const evChartData = computed(() => ({
    labels: labels.value,
    datasets: [levelDataset('battery', true)],
  }))

  const tyreChartData = computed(() => {
    const u = units.value
    return {
      labels: labels.value,
      datasets: TYRE_SERIES.map(({ position, read, color }) =>
        lineDataset(
          `${t(`vehicle.diagram.tyrePositionShort.${position}`)} (${u.symbol('pressure')})`,
          bucketAverages(buckets.value, read).map((bar) =>
            bar === null ? null : u.convert('pressure', bar),
          ),
          color,
        ),
      ),
    }
  })

  const hybridSocChartData = computed(() => ({
    labels: labels.value,
    datasets: [levelDataset('battery', false), levelDataset('fuel', false)],
  }))

  // The same counter, read in the unit this drivetrain actually reports: kWh out of the traction
  // battery on a plug-in car, litres of fuel on a plain hybrid. See utils/energy.
  const reportsFuelCounter = computed(() => energyUnit(vehicleType.value) === 'litres')

  const dailyEnergyChartData = computed(() => ({
    labels: labels.value,
    datasets: [
      {
        label: reportsFuelCounter.value ? units.value.symbol('volume') : t('common.kwh'),
        data: bucketTotals(buckets.value, (d) => d.counterTotal).map((total) => {
          const fuel = reportsFuelCounter.value ? litres(total) : null
          const used = fuel !== null ? units.value.convert('volume', fuel) : total
          return used !== null ? roundTo2(used) : null
        }),
        borderColor: '#f59e0b',
        backgroundColor: 'rgba(245,158,11,0.7)',
      },
    ],
  }))

  // The 12V battery: the day's average, with how low it sagged and how high the car charged it.
  const voltage = computed(() => ({
    low: bucketMins(buckets.value, (d) => d.batteryVoltageMin ?? d.batteryVoltage),
    avg: bucketAverages(buckets.value, (d) => d.batteryVoltage).map((v) =>
      v === null ? null : roundTo2(v),
    ),
    high: bucketMaxes(buckets.value, (d) => d.batteryVoltageMax ?? d.batteryVoltage),
  }))
  const voltageChartData = computed(() => ({
    labels: labels.value,
    datasets: [
      extremeDataset(t('statistics.series.low'), voltage.value.low, '#ef4444'),
      lineDataset(
        t('statistics.series.average'),
        voltage.value.avg,
        '#f59e0b',
        'rgba(245,158,11,0.1)',
      ),
      extremeDataset(t('statistics.series.high'), voltage.value.high, '#10b981'),
    ],
  }))
  const voltageOptions = computed(() =>
    chartOptions(2.6, true, fittedRange([...voltage.value.low, ...voltage.value.high], 2)),
  )

  // Outside, its low and high around the average, and inside the car.
  const temperature = computed(() => {
    const u = units.value
    const inUnit = (values: (number | null)[]) =>
      values.map((v) => (v === null ? null : roundTo2(u.convert('temperature', v))))
    return {
      low: inUnit(bucketMins(buckets.value, (d) => d.exteriorTemperatureMin)),
      outside: inUnit(bucketAverages(buckets.value, (d) => d.exteriorTemperature ?? null)),
      high: inUnit(bucketMaxes(buckets.value, (d) => d.exteriorTemperatureMax)),
      inside: inUnit(bucketAverages(buckets.value, (d) => d.interiorTemperature ?? null)),
    }
  })
  // The unit is in the title, so the legend stays short enough for a phone.
  const temperatureChartData = computed(() => {
    const { low, outside, high, inside } = temperature.value
    return {
      labels: labels.value,
      datasets: [
        extremeDataset(t('statistics.series.outsideLow'), low, '#3b82f6'),
        lineDataset(t('statistics.series.outside'), outside, '#0ea5e9'),
        extremeDataset(t('statistics.series.outsideHigh'), high, '#ef4444'),
        lineDataset(t('statistics.series.inside'), inside, '#8b5cf6'),
      ],
    }
  })
  const temperatureOptions = computed(() => chartOptions(2.2, true, {}))

  const range = computed(() =>
    bucketAverages(buckets.value, (d) => d.electricRangeKm ?? null).map((v) =>
      v === null ? null : Math.round(units.value.convert('distance', v)),
    ),
  )
  const rangeChartData = computed(() => ({
    labels: labels.value,
    datasets: [
      lineDataset(units.value.symbol('distance'), range.value, '#10b981', 'rgba(16,185,129,0.1)'),
    ],
  }))

  const distance = computed(() =>
    bucketTotals(buckets.value, (d) => d.distanceKm ?? null).map((v) =>
      v === null ? null : roundTo2(units.value.convert('distance', v)),
    ),
  )
  const distanceChartData = computed(() => ({
    labels: labels.value,
    datasets: [
      {
        label: units.value.symbol('distance'),
        data: distance.value,
        borderColor: '#3b82f6',
        backgroundColor: 'rgba(59,130,246,0.7)',
      },
    ],
  }))

  // One dot per trip: its consumption against the outside temperature, with the trend through them.
  const tempPoints = computed(() => {
    const u = units.value
    return consumptionByTemperature(trips.value, vehicleType.value, capacityKwh.value)
      .map((p) => ({
        x: roundTo2(u.convert('temperature', p.temperature)),
        y: roundTo2(u.convert('energyPerDistance', p.whPerKm)),
      }))
      .filter((p) => Number.isFinite(p.y))
  })
  const consumptionTempData = computed(() => {
    const points = tempPoints.value
    const fit = linearFit(points)
    const xs = points.map((p) => p.x)
    const trend =
      fit && xs.length
        ? [Math.min(...xs), Math.max(...xs)].map((x) => ({
            x,
            y: roundTo2(fit.slope * x + fit.intercept),
          }))
        : []
    return {
      datasets: [
        {
          label: t('statistics.series.trips'),
          data: points,
          backgroundColor: 'rgba(59,130,246,0.65)',
          borderColor: '#3b82f6',
          pointRadius: 4,
          pointHoverRadius: 6,
        },
        {
          label: t('statistics.series.trend'),
          data: trend,
          type: 'line' as const,
          showLine: true,
          borderColor: '#f59e0b',
          borderDash: [6, 4],
          borderWidth: 2,
          pointRadius: 0,
          fill: false,
        },
      ],
    }
  })
  const consumptionTempOptions = computed(() => {
    const { text, grid } = chartTheme.value
    const axis = { grid: { color: grid }, border: { color: grid }, ticks: { color: text } }
    const u = units.value
    return {
      locale: numberLocale(),
      responsive: true,
      maintainAspectRatio: true,
      aspectRatio: 2.6,
      animation: false as const,
      plugins: { legend: { display: true, labels: { color: text, boxWidth: 20 } } },
      scales: {
        x: {
          ...axis,
          type: 'linear' as const,
          title: { display: true, text: u.symbol('temperature'), color: text },
        },
        y: {
          ...axis,
          title: { display: true, text: u.symbol('energyPerDistance'), color: text },
        },
      },
    }
  })

  const percentOptions = computed(() => chartOptions(2.6, false, PERCENT_AXIS))
  const pressureOptions = computed(() => {
    const u = units.value
    return chartOptions(
      2.3,
      true,
      roundedRange(u.convert('pressure', 1.5), u.convert('pressure', 3.5)),
    )
  })
  const hybridSocOptions = computed(() => chartOptions(2.6, true, PERCENT_AXIS))
  const kwhOptions = computed(() => chartOptions(2.6, false, { min: 0 }))

  return computed((): ChartDef[] => [
    {
      id: 'evChart',
      icon: CHART_ICONS.evChart,
      title: t('vehicle.evSoc'),
      description: t('statistics.chartDesc.evChart'),
      vehicleApplicable: hasLargeEv.value,
      applicable: hasLargeEv.value && hasData.value,
      type: 'line',
      data: evChartData.value,
      options: percentOptions.value,
    },
    {
      id: 'voltage12vChart',
      icon: CHART_ICONS.voltage12vChart,
      title: t('statistics.voltage12vChart'),
      description: t('statistics.chartDesc.voltage12vChart'),
      vehicleApplicable: true,
      applicable: hasAny(voltage.value.avg) || hasAny(voltage.value.low),
      type: 'line',
      data: voltageChartData.value,
      options: voltageOptions.value,
    },
    {
      id: 'tyreChart',
      icon: CHART_ICONS.tyreChart,
      title: t('vehicle.tyres'),
      description: t('statistics.chartDesc.tyreChart', {
        low: units.value.measure('pressure', 2)!.value,
        high: units.value.measure('pressure', 3)!.value,
        unit: units.value.symbol('pressure'),
      }),
      vehicleApplicable: true,
      applicable: hasData.value,
      type: 'line',
      data: tyreChartData.value,
      options: pressureOptions.value,
    },
    {
      id: 'hybridSocChart',
      icon: CHART_ICONS.hybridSocChart,
      title: t('statistics.hybridSocChart'),
      description: t('statistics.chartDesc.hybridSocChart'),
      vehicleApplicable: isHybrid.value,
      applicable: isHybrid.value && hasData.value,
      type: 'line',
      data: hybridSocChartData.value,
      options: hybridSocOptions.value,
    },
    {
      id: 'dailyKwhChart',
      icon: reportsFuelCounter.value ? 'gas-pump' : CHART_ICONS.dailyKwhChart,
      title: reportsFuelCounter.value
        ? t(`statistics.fuelChart.${grouping.value}`, { unit: units.value.symbol('volume') })
        : t(`statistics.energyChart.${grouping.value}`),
      description: reportsFuelCounter.value
        ? t('statistics.chartDesc.dailyFuelChart')
        : t('statistics.chartDesc.dailyKwhChart'),
      vehicleApplicable: countsDailyEnergy.value,
      applicable: countsDailyEnergy.value && hasData.value,
      type: 'bar',
      data: dailyEnergyChartData.value,
      options: kwhOptions.value,
    },
    {
      id: 'distanceChart',
      icon: CHART_ICONS.distanceChart,
      title: t(`statistics.distanceChart.${grouping.value}`, {
        unit: units.value.symbol('distance'),
      }),
      description: t('statistics.chartDesc.distanceChart'),
      vehicleApplicable: true,
      applicable: hasAny(distance.value),
      type: 'bar',
      data: distanceChartData.value,
      options: kwhOptions.value,
    },
    {
      id: 'rangeChart',
      icon: CHART_ICONS.rangeChart,
      title: t('statistics.rangeChart', { unit: units.value.symbol('distance') }),
      description: t('statistics.chartDesc.rangeChart'),
      vehicleApplicable: hasLargeEv.value,
      applicable: hasLargeEv.value && hasAny(range.value),
      type: 'line',
      data: rangeChartData.value,
      options: kwhOptions.value,
    },
    {
      id: 'consumptionTempChart',
      icon: CHART_ICONS.consumptionTempChart,
      title: t('statistics.consumptionTempChart'),
      description: t('statistics.chartDesc.consumptionTempChart'),
      vehicleApplicable: hasLargeEv.value,
      applicable: hasLargeEv.value && tempPoints.value.length > 0,
      type: 'scatter',
      data: consumptionTempData.value as ChartData<StatsChartType>,
      options: consumptionTempOptions.value as ChartOptions<StatsChartType>,
    },
    {
      id: 'temperatureChart',
      icon: CHART_ICONS.temperatureChart,
      title: t('statistics.temperatureChart', { unit: units.value.symbol('temperature') }),
      description: t('statistics.chartDesc.temperatureChart'),
      vehicleApplicable: true,
      applicable: hasAny(temperature.value.outside) || hasAny(temperature.value.inside),
      type: 'line',
      data: temperatureChartData.value,
      options: temperatureOptions.value,
    },
  ])
}

/** One level's chart, and whether the car reported any of that level in the period. */
export type LevelChart = Pick<ChartDef, 'title' | 'icon' | 'type' | 'data' | 'options'> & {
  hasReadings: boolean
}

/**
 * The tank's or the battery's level on its own, one point per day, week or month, drawn as the
 * statistics page draws it: what the car diagram's level dialog shows.
 */
export function useLevelChart(
  buckets: Ref<StatsBucket[]>,
  grouping: Ref<StatsGrouping>,
  level: Ref<LevelKind>,
): Ref<LevelChart> {
  const { t } = useI18n()
  const { labels, chartOptions, levelDataset } = useChartFrame(buckets, grouping)
  const options = computed(() => chartOptions(2.6, false, PERCENT_AXIS))

  return computed((): LevelChart => {
    const series = LEVEL_SERIES[level.value]
    const dataset = levelDataset(level.value, true)
    return {
      title: t(series.labelKey),
      icon: series.icon,
      type: 'line',
      data: { labels: labels.value, datasets: [dataset] },
      options: options.value,
      hasReadings: dataset.data.some((v) => v !== null),
    }
  })
}
