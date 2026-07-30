import { expect } from '@playwright/test'
import { generatePrivateKey } from 'viem/accounts'
import { builderWalletTest as test } from '../fixtures/builder-fixture.js'
import { createCollectionViaUi } from '../helpers/flows.js'
import { newWalletContext } from '../helpers/wallet-context.js'
import { withEnv } from '../../../shared/helpers/url.js'

/**
 * Negative access checks — cheap guards on the builder's permission
 * boundaries. `/curation` is committee-gated (GET /committee); collection
 * detail is gated by canSeeCollection (owner ∪ managers ∪ minters). Both
 * render the builder's NotFound ("Not found…") when denied. The signed-out
 * counterpart lives in sign-in-required.spec.ts (different fixture flavor —
 * one flavor per file, marketplace convention).
 */
test.describe('@builder access control', () => {
  test('shows not-found to a non-committee wallet on the curation page', async ({ page }) => {
    await page.goto(withEnv('curation'))
    await expect(page.getByText('Not found')).toBeVisible({ timeout: 30_000 })
  })

  test("hides another wallet's unpublished collection", async ({
    page,
    collections,
    createCollectionModal,
    browser
  }) => {
    const { detailUrl } = await createCollectionViaUi(page, collections, createCollectionModal)

    const stranger = await newWalletContext(browser, generatePrivateKey())
    await stranger.page.goto(withEnv(detailUrl))
    await expect(stranger.page.getByText('Not found')).toBeVisible({ timeout: 30_000 })
    await stranger.context.close()
  })
})

test.describe('@builder fresh account', () => {
  // lives here (ephemeral-wallet flavor) because only a fresh key guarantees
  // an account with no collections — the shared test wallet never can
  test('shows the empty state for an account with no collections', async ({ collections }) => {
    await collections.goto()
    await expect(collections.emptyStateMessage()).toBeVisible({ timeout: 30_000 })
  })
})
