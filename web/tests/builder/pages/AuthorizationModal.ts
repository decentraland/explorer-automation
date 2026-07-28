import type { Page, Locator } from '@playwright/test'

/**
 * The "Authorize" modal opened by decentraland-dapps `withAuthorizedAction`
 * before the publish fee payment when the wallet hasn't yet granted the MANA
 * allowance to CollectionManager (AuthorizedAction.PUBLISH_COLLECTION).
 * Same container as the marketplace's AuthorizationModal — this is the
 * builder-local sibling per the surface-specific-helpers rule.
 *
 * Source: decentraland-dapps/src/containers/withAuthorizedAction/AuthorizationModal
 * No testids; locators rely on the container's button accessible names.
 */
export class AuthorizationModal {
  constructor(private readonly page: Page) {}

  authorizeButton(): Locator {
    return this.page.getByRole('button', { name: /^authorize/i })
  }

  actionButton(): Locator {
    return this.page.getByRole('button', { name: /^(sign|confirm transaction|proceed|continue)$/i })
  }

  /**
   * Drives the modal end-to-end IF it opens; returns silently when the
   * allowance is already granted and the dapp proceeds straight to the
   * action. The relayer broadcast of the ERC20 approval can take minutes —
   * the action button stays disabled until it lands.
   */
  async completeIfShown(openTimeoutMs = 20_000): Promise<{ opened: boolean }> {
    const opened = await this.authorizeButton()
      .first()
      .waitFor({ state: 'visible', timeout: openTimeoutMs })
      .then(() => true)
      .catch(() => false)
    if (!opened) return { opened: false }

    await this.authorizeButton().first().click()
    await this.actionButton().first().click({ timeout: 240_000 })
    return { opened: true }
  }
}
