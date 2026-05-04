// Type augmentation for `@synthetixio/ethereum-wallet-mock/playwright`.
// The package's shipped types use `export *` from a sub-module that TypeScript
// (with NodeNext moduleResolution) doesn't follow correctly, so we re-declare
// the surface we use here. The runtime export exists — see:
//   node_modules/@synthetixio/ethereum-wallet-mock/dist/playwright/index.js
declare module '@synthetixio/ethereum-wallet-mock/playwright' {
  import type {
    TestType,
    PlaywrightTestArgs,
    PlaywrightTestOptions,
    PlaywrightWorkerArgs,
    PlaywrightWorkerOptions,
  } from '@playwright/test';

  /** Subset of the EthereumWalletMock API our specs touch. */
  export interface EthereumWalletMock {
    connectToDapp(): Promise<void>;
    importWalletFromPrivateKey(privateKey: `0x${string}`): Promise<void>;
  }

  export const ethereumWalletMockFixtures: TestType<
    PlaywrightTestArgs & PlaywrightTestOptions & { ethereumWalletMock: EthereumWalletMock },
    PlaywrightWorkerArgs & PlaywrightWorkerOptions
  >;
}
