import { describe, it, expect, vi, afterEach } from 'vitest'
import { defineComponent, h, onMounted } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import TripRouteMap from '../TripRouteMap.vue'
import type { TripPoint } from '@/services/vehicleApi'
import { fitToPoints } from '@/utils/leaflet'

// The map itself is Leaflet's business; these stand-ins keep its props in reach.
vi.mock('@vue-leaflet/vue-leaflet', () => {
  const stub = (name: string, props: string[]) =>
    defineComponent({
      name,
      props,
      inheritAttrs: false,
      setup:
        (_, { slots }) =>
        () =>
          h('div', slots.default?.()),
    })
  // The map hands a fake Leaflet map to @ready once mounted, as vue-leaflet does, so the
  // framing can be watched; `fire` stands in for Leaflet raising an event on it.
  const handlers: Record<string, () => void> = {}
  const fakeMap = {
    invalidateSize: () => {},
    on: (event: string, handler: () => void) => {
      handlers[event] = handler
    },
    fire: (event: string) => handlers[event]?.(),
    latLngToContainerPoint: () => ({ x: 100, y: 100 }),
    getSize: () => ({ x: 600, y: 260 }),
  }
  const LMap = defineComponent({
    name: 'LMap',
    props: ['zoom', 'center', 'options'],
    emits: ['ready'],
    setup(_, { slots, emit }) {
      onMounted(() => emit('ready', fakeMap))
      return () => h('div', slots.default?.())
    },
  })
  return {
    fakeMap,
    LMap,
    LPolyline: stub('LPolyline', [
      'latLngs',
      'color',
      'className',
      'weight',
      'opacity',
      'dashArray',
      'interactive',
    ]),
    LCircleMarker: stub('LCircleMarker', ['latLng', 'radius', 'className', 'interactive']),
  }
})
vi.mock('@/composables/useBasemap', () => ({ useBasemap: () => undefined }))
// The car's marker: a real element to look in, its place kept where the test can read it.
const carMarker = vi.hoisted(() => ({ at: null as unknown, removed: false }))
vi.mock('@/utils/leaflet', () => ({
  fitToPoints: vi.fn<typeof fitToPoints>(),
  L: {
    divIcon: (options: { html: string }) => options,
    marker: (at: unknown, options: { icon: { html: string } }) => {
      const element = document.createElement('div')
      element.innerHTML = options.icon.html
      carMarker.at = at
      carMarker.removed = false
      const marker = {
        addTo: () => marker,
        setLatLng: (next: unknown) => {
          carMarker.at = next
        },
        getElement: () => element,
        remove: () => {
          carMarker.removed = true
        },
      }
      return marker
    },
  },
}))

// jsdom has no layout, so neither ResizeObserver nor a frame to wait for.
vi.stubGlobal(
  'ResizeObserver',
  class {
    observe() {}
    disconnect() {}
  },
)
vi.stubGlobal('requestAnimationFrame', (callback: () => void) => {
  callback()
  return 0
})

afterEach(() => {
  vi.mocked(fitToPoints).mockClear()
})

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })

const fix = (latitude: number, speed: number | null): TripPoint => ({
  recordedAt: '2026-09-24T12:40:00.000Z',
  latitude,
  longitude: 6,
  speed,
})

function mountMap(props: Partial<InstanceType<typeof TripRouteMap>['$props']> = {}) {
  return mount(TripRouteMap, {
    props: {
      points: null,
      loading: false,
      start: [52.5, 6] as [number, number],
      end: [52.2, 6.1] as [number, number],
      tripId: 7,
      startedAt: '2026-09-24T12:32:00.000Z',
      linkToMap: true,
      ...props,
    },
    global: {
      plugins: [i18n],
      stubs: {
        FontAwesomeIcon: true,
        RouterLink: {
          props: ['to'],
          template: '<a class="router-link" :data-to="JSON.stringify(to)"><slot /></a>',
        },
      },
    },
  })
}

const lines = (wrapper: ReturnType<typeof mountMap>) =>
  wrapper.findAllComponents({ name: 'LPolyline' })

describe('TripRouteMap', () => {
  it('colours the route by the speed of each stretch', () => {
    const wrapper = mountMap({ points: [fix(52.5, 20), fix(52.4, 140), fix(52.3, 140)] })

    const colours = lines(wrapper).map((l) => l.props('color'))
    expect(colours).toHaveLength(2)
    expect(colours[0]).not.toBe(colours[1])
  })

  it('joins the ends with a straight line once the fixes could not be had', () => {
    const wrapper = mountMap()

    expect(lines(wrapper)).toHaveLength(1)
    expect(lines(wrapper)[0]!.props('latLngs')).toEqual([
      [52.5, 6],
      [52.2, 6.1],
    ])
  })

  it('draws no line while the fixes load, and says it is loading', () => {
    const wrapper = mountMap({ loading: true })

    expect(lines(wrapper)).toHaveLength(0)
    expect(wrapper.find('[role="status"]').exists()).toBe(true)
  })

  it('marks both ends', () => {
    const markers = mountMap().findAllComponents({ name: 'LCircleMarker' })

    expect(markers.map((m) => m.props('latLng'))).toEqual([
      [52.5, 6],
      [52.2, 6.1],
    ])
  })

  it('links to the trip on the full map, unless it is hidden from it', () => {
    const link = mountMap().find('.router-link')

    expect(JSON.parse(link.attributes('data-to')!)).toEqual({
      name: 'map',
      query: { trip: 7, at: '2026-09-24T12:32:00.000Z' },
    })
    expect(mountMap({ linkToMap: false }).find('.router-link').exists()).toBe(false)
  })

  it('frames both ends clear of the map button and the attribution, again on a resize', async () => {
    const { fakeMap } = (await import('@vue-leaflet/vue-leaflet')) as unknown as {
      fakeMap: { fire: (event: string) => void }
    }
    mountMap({ points: [fix(52.5, 20), fix(52.2, 60)] })
    await flushPromises()

    expect(fitToPoints).toHaveBeenCalledTimes(1)
    const [, framed, padding] = vi.mocked(fitToPoints).mock.calls[0]!
    expect(framed).toEqual([
      [52.5, 6],
      [52.2, 6],
    ])
    expect(padding).toEqual({ topLeft: [36, 56], bottomRight: [36, 48] })

    fakeMap.fire('resize')

    expect(fitToPoints).toHaveBeenCalledTimes(2)
  })

  it('shows the car where it was at the moment picked on the chart, the route ahead greyed', async () => {
    const timed = (time: string, latitude: number, speed: number): TripPoint => ({
      recordedAt: `2026-09-24T12:${time}:00.000Z`,
      latitude,
      longitude: 6,
      speed,
    })
    const points = [timed('40', 52.5, 20), timed('42', 52.4, 60), timed('44', 52.3, 60)]
    const wrapper = mountMap({ points })
    await flushPromises()
    expect(wrapper.findAll('.trip-route-map__ahead')).toHaveLength(0)

    await wrapper.setProps({
      cursor: { time: Date.parse('2026-09-24T12:41:00.000Z'), label: '13:41 · 37 mph' },
    })
    await flushPromises()

    const ahead = lines(wrapper).find((l) => l.props('className') === 'trip-route-map__ahead')!
    expect(ahead.props('latLngs')).toEqual([
      [52.45, 6],
      [52.4, 6],
      [52.3, 6],
    ])
    expect(carMarker.at).toEqual([52.45, 6])
    expect(carMarker.removed).toBe(false)

    await wrapper.setProps({ cursor: null })
    await flushPromises()
    expect(carMarker.removed).toBe(true)
  })

  it('has no car to show on a trip saved without a route', async () => {
    const wrapper = mountMap({
      noRoute: true,
      cursor: { time: Date.parse('2026-09-24T12:41:00.000Z'), label: '13:41' },
    })
    await flushPromises()

    expect(lines(wrapper).some((l) => l.props('className') === 'trip-route-map__ahead')).toBe(false)
  })
})
