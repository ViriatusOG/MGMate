import { computed, ref } from 'vue'
import type { Ref } from 'vue'
import { asError } from '@/utils/errors'

/**
 * Counts in-flight async actions so `loading` stays true until the last one settles, and runs
 * each action with its own error ref. Concurrent calls (e.g. Promise.all on mount) therefore
 * cannot have one action's success silently overwrite another action's error. Shared by the
 * vehicle and maintenance stores.
 */
export function useLoadingTracker() {
  const loadingCount = ref(0)
  const loading = computed(() => loadingCount.value > 0)

  /**
   * `isCurrent`, when given, says whether this call is still the newest of its kind: a superseded
   * call that fails afterwards must not put its error over the newer call's result.
   */
  async function withLoading(
    errorRef: Ref<Error | null>,
    fn: () => Promise<void>,
    isCurrent: () => boolean = () => true,
  ) {
    loadingCount.value++
    errorRef.value = null
    try {
      await fn()
    } catch (e) {
      if (isCurrent()) errorRef.value = asError(e)
    } finally {
      loadingCount.value--
    }
  }

  return { loadingCount, loading, withLoading }
}
