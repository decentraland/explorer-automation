import { privateKeyToAccount } from 'viem/accounts';
import type { Page, Route } from '@playwright/test';
import type { EthereumWalletMock } from '@synthetixio/ethereum-wallet-mock/playwright';

/**
 * Intercepts every Decentraland catalyst `/lambdas/profiles/<address>` call
 * and returns "Profile not found", so the auth dapp's `useEnsureProfile`
 * predicate (`profile.avatars[0].name !== undefined`) evaluates false and
 * routes the user to `/auth/quick-setup` instead of the homepage.
 *
 * Without this, even a freshly-generated wallet often hits the homepage
 * because catalysts return placeholder/default profiles for unknown addresses.
 *
 * Call BEFORE clicking the MetaMask button. Skip this for "recurrent user"
 * tests where you WANT the dapp to find a profile.
 *
 * Returns an unmock function that removes the interception, useful for
 * register-then-recurrent flows that switch behavior mid-test.
 */
export async function mockNoProfileOnCatalysts(page: Page): Promise<() => Promise<void>> {
  // Only intercept lookups by 0x-address. The dapp also fetches `defaultN`
  // pseudo-profiles on quick-setup to populate the "randomize" presets;
  // intercepting those would break the avatar picker UI.
  const matcher = (url: URL): boolean =>
    /\/lambdas\/profiles?\/0x[a-f0-9]{40}\b/i.test(url.toString());
  const handler = (route: Route): Promise<void> =>
    route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Not Found', message: 'Profile not found' }),
    });
  await page.route(matcher, handler);
  return async () => {
    await page.unroute(matcher, handler);
  };
}

// Pages that have had the one-time wallet plumbing installed. Lets
// `setupMockedWallet` be safely re-called on the same page (e.g. for a
// register-then-recurrent flow) — it skips re-exposing `__signMessage` and
// re-adding the polyfill init script, both of which would otherwise error or
// stack.
const installedOn = new WeakSet<Page>();

/**
 * Sets up a mocked Ethereum wallet on the page, navigates to `/auth/login`,
 * and configures Web3Mock to advertise the given private key's address. After
 * this, clicking the MetaMask button on the auth screen drives the full
 * sign-in flow with real `viem`-backed signatures.
 *
 * Idempotent on the same page — calling twice (e.g. to re-login as a recurrent
 * user) re-navigates and re-binds the mock state, but only installs the
 * one-time plumbing (polyfill + signer binding) on the first call. The signer
 * is closure-bound to the FIRST call's privateKey, so use the same key across
 * calls (the recurrent flow does, by design).
 */
export async function setupMockedWallet(
  page: Page,
  ethereumWalletMock: EthereumWalletMock,
  { redirectTo, privateKey }: { redirectTo?: string; privateKey: `0x${string}` },
): Promise<void> {
  const account = privateKeyToAccount(privateKey);

  if (!installedOn.has(page)) {
    // Polyfill legacy `window.ethereum.enable()` (used by some wallet detection
    // libraries the dapp depends on).
    await page.context().addInitScript(() => {
      const interval = setInterval(() => {
        const w = window as unknown as {
          ethereum?: {
            enable?: () => Promise<unknown>;
            request: (a: { method: string }) => Promise<unknown>;
          };
        };
        if (w.ethereum && !w.ethereum.enable) {
          w.ethereum.enable = () => w.ethereum!.request({ method: 'eth_requestAccounts' });
          clearInterval(interval);
        }
      }, 50);
    });

    await page.exposeFunction('__signMessage', async (hex: string) => {
      const message = Buffer.from(hex.slice(2), 'hex').toString('utf-8');
      return account.signMessage({ message });
    });

    installedOn.add(page);
  }

  const url = new URL('https://decentraland.org/auth/login');
  if (redirectTo) url.searchParams.set('redirectTo', redirectTo);
  await page.goto(url.toString(), { waitUntil: 'load' });

  await ethereumWalletMock.connectToDapp();
  await ethereumWalletMock.importWalletFromPrivateKey(privateKey);

  // Route `personal_sign` through our viem signer. The mock's default request
  // handler returns a stub signature; the dapp validates it cryptographically
  // against the account's address, so we have to produce a real signature from
  // the same private key.
  await page.evaluate(() => {
    type Eth = { request: (args: { method: string; params?: unknown[] }) => Promise<unknown> };
    const w = window as unknown as {
      ethereum: Eth;
      __signMessage: (hex: string) => Promise<string>;
    };
    const original = w.ethereum.request.bind(w.ethereum);
    w.ethereum.request = async (args) => {
      if (
        args.method === 'personal_sign' &&
        Array.isArray(args.params) &&
        typeof args.params[0] === 'string'
      ) {
        return w.__signMessage(args.params[0]);
      }
      return original(args);
    };
  });
}
