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
}
