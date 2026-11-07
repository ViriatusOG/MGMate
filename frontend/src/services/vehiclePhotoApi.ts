import { apiUrl, request } from '@/services/apiCore'

/** Whether the car has an uploaded photo, and when it last changed. */
export interface VehiclePhotoInfo {
  hasPhoto: boolean
  updatedAt: string | null
}

/** Mirrors VehiclePhoto.MaxBytes. */
export const VEHICLE_PHOTO_MAX_BYTES = 3 * 1024 * 1024

export const vehiclePhotoApi = {
  info: (vin: string) => request<VehiclePhotoInfo>(`/api/vehicles/${vin}/photo/info`),
  /** The image's address; the upload time in it lets the browser cache it until it changes. */
  url: (vin: string, updatedAt: string) =>
    apiUrl(`/api/vehicles/${vin}/photo?v=${encodeURIComponent(updatedAt)}`),
  upload: (vin: string, image: Blob) =>
    request<VehiclePhotoInfo>(`/api/vehicles/${vin}/photo`, {
      method: 'PUT',
      headers: { 'Content-Type': image.type || 'application/octet-stream' },
      body: image,
    }),
  remove: (vin: string) => request<void>(`/api/vehicles/${vin}/photo`, { method: 'DELETE' }),
}
