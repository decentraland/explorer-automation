/**
 * Appends `?env=<env>` (or `&env=<env>`) to a URL/path, unless `env` is empty
 * or the URL already specifies an env. Marketplace's @dcl/ui-env reads the
 * query param ONCE at config-init, so it must be present on the first hard
 * navigation of the SPA (and on any subsequent hard nav, e.g. an auth
 * redirect that returns to a fresh page).
 */
export function withEnv(urlOrPath: string, env = process.env.MARKETPLACE_ENV ?? 'dev'): string {
  if (!env) return urlOrPath
  const sep = urlOrPath.includes('?') ? '&' : '?'
  if (/[?&]env=/i.test(urlOrPath)) return urlOrPath
  return `${urlOrPath}${sep}env=${env}`
}
