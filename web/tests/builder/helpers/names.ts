/**
 * Unique, URN-safe collection name. Builder collection names are capped at 32
 * chars and must be unique server-side ("Name already in use"); published
 * collections are permanent on dev, so the prefix keeps QA runs identifiable
 * (and grep-able) among real dev collections.
 */
export function uniqueCollectionName(): string {
  const prefix = process.env.TEST_COLLECTION_PREFIX ?? 'QA'
  const stamp = Date.now().toString(36)
  const salt = Math.random().toString(36).slice(2, 6)
  return `${prefix}${stamp}${salt}`
}
