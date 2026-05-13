import { marketplaceTest as test } from '../fixtures/wallet-fixture.js'

const { expect } = test

test.describe('@marketplace public browse', () => {
  test('loads the browse page and shows asset cards', async ({ browse }) => {
    await browse.goto()
    await browse.waitForResults()

    await expect(browse.assetCards().first()).toBeVisible()
    expect(await browse.assetCards().count()).toBeGreaterThan(0)
  })

  test('clicking an asset card navigates to its detail page', async ({ page, browse }) => {
    await browse.goto()
    await browse.waitForResults()

    await browse.openAsset(0)
    await expect(page).toHaveURL(/\/contracts\/0x[a-f0-9]+\/(items|tokens)\/.+/i)
  })
})
