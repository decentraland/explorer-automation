/**
 * Generic concurrency-limited job runner.
 *
 * - Runs `jobs` with at most `concurrency` in flight.
 * - `failFast: true` aborts remaining jobs on the first error AND signals
 *   in-flight jobs (via the AbortSignal passed to `fn`) so they can cancel
 *   their underlying work; without that, a fail-fast run blocks until every
 *   in-flight job naturally completes.
 * - Per-job stdout/stderr is the caller's responsibility; the pool only orchestrates.
 */
export interface Job<T> {
  label: string
  fn: (signal: AbortSignal) => Promise<T>
}

export interface JobResult<T> {
  label: string
  ok: boolean
  value?: T
  error?: Error
  ms: number
}

export interface PoolOptions {
  concurrency: number
  failFast: boolean
  onStart?: (label: string) => void
  onFinish?: (result: JobResult<unknown>) => void
}

export async function runPool<T>(jobs: Job<T>[], options: PoolOptions): Promise<JobResult<T>[]> {
  const { concurrency, failFast, onStart, onFinish } = options
  const results: JobResult<T>[] = new Array(jobs.length)
  const abortCtrl = new AbortController()
  let next = 0
  let firstError: Error | undefined

  async function worker(): Promise<void> {
    while (next < jobs.length) {
      if (failFast && abortCtrl.signal.aborted) return
      const i = next++
      const { label, fn } = jobs[i]
      const started = Date.now()
      onStart?.(label)
      try {
        const value = await fn(abortCtrl.signal)
        const result: JobResult<T> = { label, ok: true, value, ms: Date.now() - started }
        results[i] = result
        onFinish?.(result as JobResult<unknown>)
      } catch (err) {
        const error = err instanceof Error ? err : new Error(String(err))
        const result: JobResult<T> = { label, ok: false, error, ms: Date.now() - started }
        results[i] = result
        onFinish?.(result as JobResult<unknown>)
        if (failFast) {
          firstError ??= error
          abortCtrl.abort(error)
        }
      }
    }
  }

  const workers = Array.from({ length: Math.max(1, concurrency) }, () => worker())
  await Promise.all(workers)

  if (failFast && firstError) throw firstError
  return results
}
