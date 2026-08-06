import { ethereumWalletMockFixtures } from '@synthetixio/ethereum-wallet-mock/playwright'
import { contextWithDiagnostics } from './base-test.js'

/**
 * Playwright `test` instance extended with Synpress' mocked-wallet fixture.
 * Wallet specs import this as `walletTest` and gain `ethereumWalletMock` on the
 * test context. OTP / non-wallet specs use `test` from `base-test.ts` instead.
 *
 * The mock injects a fake `window.ethereum` provider into every page launched
 * by this fixture, so don't use it for tests that need a clean browser state.
 *
 * The `context` override layers in the shared diagnostics fixture (see
 * base-test.ts): the CF Access route — so `.zone` / `.today` runs
 * authenticate against Cloudflare Access without leaking the service-token
 * headers onto cross-origin requests — plus the verbose browser log attached
 * on failure.
 */
export const walletTest = ethereumWalletMockFixtures.extend({
  context: contextWithDiagnostics
})
