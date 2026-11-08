import { request } from '@/services/apiCore'

/** How the gateway's sign-in to the MG account last went, by the Worker's latest heartbeat. */
export interface GatewaySignIn {
  failing: boolean
  lastSignInAt: string | null
  lastFailureAt: string | null
}

export const gatewayApi = {
  signIn: () => request<GatewaySignIn>('/api/gateway/sign-in'),
}
