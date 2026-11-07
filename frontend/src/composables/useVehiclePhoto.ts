import { computed, reactive, ref, watch, type Ref } from 'vue'
import { VEHICLE_PHOTO_MAX_BYTES, vehiclePhotoApi } from '@/services/vehiclePhotoApi'

// Per car: when its photo last changed (null for none), shared by every card showing the car so
// an upload in one shows in all. A car not yet asked about is missing.
const updatedAt = reactive(new Map<string, string | null>())
const pending = new Map<string, Promise<void>>()

function load(vin: string): Promise<void> {
  let p = pending.get(vin)
  if (!p) {
    p = vehiclePhotoApi
      .info(vin)
      .then((info) => void updatedAt.set(vin, info.hasPhoto ? info.updatedAt : null))
      .catch(() => void updatedAt.set(vin, null))
    pending.set(vin, p)
  }
  return p
}

/** Forgets what was loaded, for tests. */
export function resetVehiclePhotoCache() {
  updatedAt.clear()
  pending.clear()
}

// Every photo is redrawn before it goes up: a cut-out car is trimmed to its outline, so the card
// shows the car and not the empty space around it, and a large photo is drawn smaller. Where the
// browser cannot draw it (or in tests), the file goes up as picked.
const MAX_WIDTH = 1600
const TRIM_PADDING = 0.02

// The box around everything not fully transparent, or null when nothing is.
function opaqueBox(data: Uint8ClampedArray, width: number, height: number) {
  let top = height
  let left = width
  let right = -1
  let bottom = -1
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      if (data[(y * width + x) * 4 + 3]! > 8) {
        if (y < top) top = y
        if (y > bottom) bottom = y
        if (x < left) left = x
        if (x > right) right = x
      }
    }
  }
  return right < 0 ? null : { left, top, width: right - left + 1, height: bottom - top + 1 }
}

async function prepare(file: File): Promise<Blob> {
  try {
    const bitmap = await createImageBitmap(file)
    const full = document.createElement('canvas')
    full.width = bitmap.width
    full.height = bitmap.height
    const ctx = full.getContext('2d')!
    ctx.drawImage(bitmap, 0, 0)
    const pixels = ctx.getImageData(0, 0, full.width, full.height).data
    const box = opaqueBox(pixels, full.width, full.height) ?? {
      left: 0,
      top: 0,
      width: full.width,
      height: full.height,
    }
    const trimmed = box.width < full.width || box.height < full.height
    if (!trimmed && file.size <= VEHICLE_PHOTO_MAX_BYTES && bitmap.width <= MAX_WIDTH) return file

    const pad = trimmed ? Math.round(Math.max(box.width, box.height) * TRIM_PADDING) : 0
    const scale = Math.min(1, MAX_WIDTH / (box.width + pad * 2))
    const out = document.createElement('canvas')
    out.width = Math.round((box.width + pad * 2) * scale)
    out.height = Math.round((box.height + pad * 2) * scale)
    out
      .getContext('2d')!
      .drawImage(
        full,
        box.left,
        box.top,
        box.width,
        box.height,
        pad * scale,
        pad * scale,
        box.width * scale,
        box.height * scale,
      )
    return await new Promise((resolve, reject) =>
      out.toBlob((b) => (b ? resolve(b) : reject(new Error('draw'))), 'image/webp', 0.9),
    )
  } catch {
    return file
  }
}

/** The car's uploaded photo: its address (null for none), and uploading or removing it. */
export function useVehiclePhoto(vin: Ref<string | null | undefined>) {
  const busy = ref(false)
  const error = ref<unknown>(null)

  watch(
    vin,
    (v) => {
      if (v && !updatedAt.has(v)) load(v)
    },
    { immediate: true },
  )

  const url = computed(() => {
    const v = vin.value
    const at = v ? updatedAt.get(v) : null
    return v && at ? vehiclePhotoApi.url(v, at) : null
  })

  async function run(action: (v: string) => Promise<void>) {
    const v = vin.value
    if (!v) return
    busy.value = true
    error.value = null
    try {
      await action(v)
    } catch (e) {
      error.value = e
    } finally {
      busy.value = false
    }
  }

  const upload = (file: File) =>
    run(async (v) => {
      const info = await vehiclePhotoApi.upload(v, await prepare(file))
      updatedAt.set(v, info.hasPhoto ? info.updatedAt : null)
    })

  const remove = () =>
    run(async (v) => {
      await vehiclePhotoApi.remove(v)
      updatedAt.set(v, null)
    })

  return { url, busy, error, upload, remove }
}
