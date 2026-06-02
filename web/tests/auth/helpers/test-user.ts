import { randomBytes } from 'node:crypto'
import { optionalEnv } from '../../../shared/helpers/env.js'

/**
 * Generates a fresh Decentraland in-world username for new-account signup
 * specs. Each call returns a distinct value so reruns and parallel workers
 * never collide on the same profile.
 *
 * The prefix defaults to `QA` and can be overridden by `TEST_USERNAME_PREFIX`
 * — useful when distinguishing profiles created by a particular runner
 * (e.g. a developer's local box vs. CI) or when running alongside another
 * QA suite that also writes test profiles.
 */
export const uniqueUsername = (): string =>
  `${optionalEnv('TEST_USERNAME_PREFIX') ?? 'QA'}${randomBytes(3).toString('hex')}`
