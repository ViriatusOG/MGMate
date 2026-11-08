import { describe, it, expect, afterEach, vi } from 'vitest'
import { effectScope, nextTick, ref } from 'vue'
import type { GatewaySignIn } from '@/services/gatewayApi'

const mockSignIn = vi.fn<() => Promise<GatewaySignIn>>()

vi.mock('@/services/gatewayApi', () => ({ gatewayApi: { signIn: mockSignIn } }))

const failing: GatewaySignIn = {
  failing: true,
  lastSignInAt: '2026-11-08T07:40:00Z',
  lastFailureAt: '2026-11-08T08:03:38Z',
}

async function flush() {
  await nextTick()
  await Promise.resolve()
  await nextTick()
}

describe('useGatewaySignIn', () => {
  afterEach(() => {
    mockSignIn.mockReset()
    vi.useRealTimers()
  })

  it('asks while active and asks again every minute', async () => {
    vi.useFakeTimers()
    mockSignIn.mockResolvedValue(failing)
    const { useGatewaySignIn } = await import('@/composables/useGatewaySignIn')
    const scope = effectScope()
    const result = scope.run(() => useGatewaySignIn(ref(true)))!

    await flush()
    expect(result.status.value?.failing).toBe(true)
    expect(mockSignIn).toHaveBeenCalledTimes(1)

    await vi.advanceTimersByTimeAsync(60_000)
    expect(mockSignIn).toHaveBeenCalledTimes(2)
    scope.stop()
  })

  it('does not ask while inactive, and forgets the answer on sign-out', async () => {
    mockSignIn.mockResolvedValue(failing)
    const { useGatewaySignIn } = await import('@/composables/useGatewaySignIn')
    const active = ref(false)
    const scope = effectScope()
    const result = scope.run(() => useGatewaySignIn(active))!

    await flush()
    expect(mockSignIn).not.toHaveBeenCalled()

    active.value = true
    await flush()
    expect(result.status.value).toEqual(failing)

    active.value = false
    await flush()
    expect(result.status.value).toBeNull()
    scope.stop()
  })

  it('keeps showing nothing when the API cannot answer', async () => {
    mockSignIn.mockRejectedValue(new Error('404'))
    const { useGatewaySignIn } = await import('@/composables/useGatewaySignIn')
    const scope = effectScope()
    const result = scope.run(() => useGatewaySignIn(ref(true)))!

    await flush()
    expect(result.status.value).toBeNull()
    scope.stop()
  })
})
