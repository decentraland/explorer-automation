import { expect } from '@playwright/test'
import { builderTest as test } from '../fixtures/builder-fixture.js'

/**
 * Signed-out gate — uses the POM-only fixture flavor (no wallet mock, no SSO
 * seed), so the dapp treats the visitor as anonymous.
 */
test.describe('@builder sign-in required', () => {
  test('prompts a signed-out visitor to sign in on the collections page', async ({ collections }) => {
    await collections.goto()
    await expect(collections.signInRequiredMessage()).toBeVisible({ timeout: 30_000 })
    await expect(collections.signInLink()).toBeVisible({ timeout: 15_000 })
  })
})
