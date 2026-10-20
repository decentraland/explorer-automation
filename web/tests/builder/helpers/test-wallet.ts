import { generatePrivateKey } from 'viem/accounts'
import { optionalEnv } from '../../../shared/helpers/env.js'

/**
 * Key for builder flows that create collections. Team decision (2026-07-28):
 * reuse WALLET_A — the marketplace pool wallet — as the builder creator on
 * dev, accepting the cross-suite coupling web/CLAUDE.md warns about because a
 * fixed wallet keeps the dev DB sweepable (ephemeral keys from crashed runs
 * leave undeletable rows) and a dev publish costs ~1 MANA, negligible for the
 * pool. Falls back to an ephemeral key so zero-env runs (PR CI without
 * secrets) still pass; those rely on fixture teardown for cleanup instead.
 */
export function builderTestWalletKey(): `0x${string}` {
  return (optionalEnv('WALLET_A_PRIVATE_KEY') as `0x${string}` | undefined) ?? generatePrivateKey()
}
