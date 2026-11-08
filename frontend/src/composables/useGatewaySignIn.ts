import { onScopeDispose, shallowRef, watch, type Ref } from 'vue'
import { gatewayApi, type GatewaySignIn } from '@/services/gatewayApi'

// The gateway tries to sign in again about once a minute while it fails, so asking more often
// would only repeat the answer.
const POLL_MS = 60_000

/**
 * Whether the gateway can sign in to the MG account. When it cannot, no new car data arrives and
 * the car's last state (Online, say) would otherwise keep showing as if it were current. Asked
 * while signed in to MGMate; failing to ask shows nothing, since an older API lacks the endpoint.
 */
export function useGatewaySignIn(active: Ref<boolean>) {
  const status = shallowRef<GatewaySignIn | null>(null)
  let timer: ReturnType<typeof setInterval> | undefined

  async function refresh() {
    try {
      status.value = await gatewayApi.signIn()
    } catch {
      // Offline, signed out, or an older API: keep the last answer.
    }
  }

  function stop() {
    if (timer !== undefined) clearInterval(timer)
    timer = undefined
  }

  watch(
    active,
    (on) => {
      stop()
      if (!on) {
        status.value = null
        return
      }
      void refresh()
      timer = setInterval(refresh, POLL_MS)
    },
    { immediate: true },
  )

  onScopeDispose(stop)

  return { status, refresh }
}
