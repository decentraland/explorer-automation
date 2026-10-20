// Type augmentation for `@synthetixio/ethereum-wallet-mock/playwright`.
// Under NodeNext resolution the package's `export *` re-export of
// `ethereumWalletMockFixtures` doesn't surface in consumers, so we declare it
// explicitly here. The runtime JS does export this symbol — this only affects
// the typechecker.

import 'viem'
import type {
  TestType,
  PlaywrightTestArgs,
  PlaywrightTestOptions,
  PlaywrightWorkerArgs,
  PlaywrightWorkerOptions
} from '@playwright/test'

declare module '@synthetixio/ethereum-wallet-mock/playwright' {
  // eslint-disable-next-line @typescript-eslint/consistent-type-imports
  type EWM = import('@synthetixio/ethereum-wallet-mock/playwright').EthereumWalletMock

  export const ethereumWalletMockFixtures: TestType<
    PlaywrightTestArgs & PlaywrightTestOptions & { ethereumWalletMock: EWM },
    PlaywrightWorkerArgs & PlaywrightWorkerOptions
  >

  /**
   * Absolute path to the @depay/web3-mock UMD bundle and the in-page bootstrap
   * Synpress injects per context (dist/playwright/index.js:279). Exported at
   * runtime but missing from the package's typings; used by
   * tests/builder/helpers/wallet-context.ts to reproduce the context init for
   * secondary (multi-wallet) browser contexts.
   */
  export const web3MockPath: string
  export function mockEthereum(): void
}

declare global {
  interface Window {
    /**
     * Init-script handshake flag set by `installInjectedWalletMock` once it
     * has wrapped `window.ethereum.request`. `setupBroadcastWallet` waits for
     * this flag before layering its own wrapper so the broadcast layer always
     * sits on top of the mock layer, not the raw Synpress handler.
     */
    __injectedWalletMockInstalled?: boolean
  }
}

export {}
