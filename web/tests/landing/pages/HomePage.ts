import type { Page } from '@playwright/test'

/**
 * Page Object for the post-login Decentraland homepage (`https://decentraland.org/`).
 * Same URL as the public landing page but rendered with the logged-in
 * navbar and dashboard. Auth specs use `waitFor()` to assert the dapp
 * redirected back to `/` after a successful login.
 *
 * The public-hero download CTA lives on `LandingPage` — it's reachable
 * from a logged-out visit to `/`, not from this post-login state.
 */
export class HomePage {
  constructor(private readonly page: Page) {}

  async waitFor(timeoutMs = 30_000): Promise<void> {
    await this.page.waitForURL(url => /decentraland\.org\/?(\?.*)?$/.test(url.toString()), {
      timeout: timeoutMs
    })
  }
}
