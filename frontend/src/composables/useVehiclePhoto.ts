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

// A photo over the limit is drawn smaller first: a card never needs more than this width.
const MAX_WIDTH = 1600

async function shrink(file: File): Promise<Blob> {
  if (file.size <= VEHICLE_PHOTO_MAX_BYTES) return file
  const bitmap = await createImageBitmap(file)
  const scale = Math.min(1, MAX_WIDTH / bitmap.width)
  const canvas = document.createElement('canvas')
  canvas.width = Math.round(bitmap.width * scale)
  canvas.height = Math.round(bitmap.height * scale)
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  return new Promise((resolve, reject) =>
    canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('shrink'))), 'image/webp', 0.85),
  )
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
      const info = await vehiclePhotoApi.upload(v, await shrink(file))
      updatedAt.set(v, info.hasPhoto ? info.updatedAt : null)
    })

  const remove = () =>
    run(async (v) => {
      await vehiclePhotoApi.remove(v)
      updatedAt.set(v, null)
    })

  return { url, busy, error, upload, remove }
}
