import { ethereumWalletMockFixtures } from '@synthetixio/ethereum-wallet-mock/playwright'

/**
 * Playwright `test` instance extended with Synpress' mocked-wallet fixture.
 * Wallet specs import this as `walletTest` and gain `ethereumWalletMock` on the
 * test context. OTP / non-wallet specs continue to use plain `@playwright/test`.
 *
 * The mock injects a fake `window.ethereum` provider into every page launched
 * by this fixture, so don't use it for tests that need a clean browser state.
 */
export const walletTest = ethereumWalletMockFixtures
