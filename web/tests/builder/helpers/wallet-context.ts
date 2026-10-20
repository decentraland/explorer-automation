import { readFileSync } from 'node:fs'
import type { Browser, BrowserContext, Page } from '@playwright/test'
import { mockEthereum, web3MockPath } from '@synthetixio/ethereum-wallet-mock/playwright'
import { installCfAccessRoute } from '../../../shared/fixtures/base-test.js'
import { setupBuilderWallet, type SetupBuilderWalletOptions } from './wallet-setup.js'

/**
 * Opens a SECOND browser context signed in as `privateKey` — for multi-wallet
 * specs (owner vs stranger, creator vs curator). Synpress's
 * `ethereumWalletMockFixtures` only wires Web3Mock into the default context,
 * so this reproduces its context-level init verbatim
 * (@synthetixio/ethereum-wallet-mock dist/playwright/index.js:279: inject the
 * web3-mock UMD bundle + invoke `mockEthereum`), then layers the standard
 * builder wallet setup on top.
 *
 * Callers own the context: `await context.close()` when done (fixtures do
 * this in teardown).
 */
export async function newWalletContext(
  browser: Browser,
  privateKey: `0x${string}`,
  options: SetupBuilderWalletOptions = {}
): Promise<{ context: BrowserContext; page: Page; address: string }> {
  const context = await browser.newContext()
  // fixture-owned contexts get this via contextWithDiagnostics; a hand-made
  // context must install it itself or .zone/.today runs hit the CF wall
  await installCfAccessRoute(context)
  await context.addInitScript({
    content: `${readFileSync(web3MockPath, 'utf-8')}\n(${mockEthereum.toString()})();`
  })
  const page = await context.newPage()
  const { address } = await setupBuilderWallet(page, privateKey, options)
  return { context, page, address }
}
