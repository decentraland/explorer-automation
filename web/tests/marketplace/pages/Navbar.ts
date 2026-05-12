import type { Page, Locator } from '@playwright/test'

export class Navbar {
  constructor(private readonly page: Page) {}

  /**
   * "SIGN IN" button shown when no wallet is connected. Once connected,
   * marketplace replaces it with a network selector + avatar + balance widgets.
   */
  signInButton(): Locator {
    return this.page.getByRole('button', { name: /^sign in$/i })
  }

  /**
   * Top-bar `<nav>` container. Marketplace renders two regions with role
   * `navigation` (the top bar and an expandable secondary menu); we always
   * want the first.
   */
  private container(): Locator {
    return this.page.getByRole('navigation').first()
  }

  /**
   * "User menu" button shown only when a wallet is connected. Preferred over
   * a chain-chip locator because it's binary: a "Wrong Network" modal hides
   * the chain chip but leaves the user menu in the navbar.
   */
  userMenu(): Locator {
    return this.container().getByRole('button', { name: /user menu/i })
  }

  async waitForConnected(timeoutMs = 30_000): Promise<void> {
    await this.userMenu().waitFor({ state: 'visible', timeout: timeoutMs })
  }

  async waitForDisconnected(timeoutMs = 30_000): Promise<void> {
    await this.signInButton().waitFor({ state: 'visible', timeout: timeoutMs })
  }

  /**
   * Opens the user menu and clicks the Log Out / Disconnect entry. The
   * dropdown item may render as a `menuitem` or a button, and the wording
   * varies ("Log Out", "Sign Out", "Disconnect"). Accept all via a broad
   * accessible-name regex.
   */
  async clickLogout(): Promise<void> {
    await this.userMenu().click()
    // The dropdown's Log Out entry is rendered as plain text inside a non-
    // semantic div (no `role="menuitem"` / button). Use getByText with an
    // exact match to avoid catching unrelated occurrences of the word, and
    // .first() in case the dropdown duplicates entries between desktop and
    // mobile layouts.
    const matcher = /^(log\s*out|sign\s*out|disconnect)$/i
    const logout = this.page.getByText(matcher).first()
    await logout.waitFor({ state: 'visible', timeout: 10_000 })
    await logout.click()
  }
}
