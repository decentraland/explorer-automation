import type { Page, Locator } from '@playwright/test'
import { withEnv } from '../../../shared/helpers/url.js'

export class SignInPage {
  constructor(private readonly page: Page) {}

  async goto(redirectTo?: string): Promise<void> {
    const path = redirectTo ? `sign-in?redirectTo=${encodeURIComponent(redirectTo)}` : 'sign-in'
    await this.page.goto(withEnv(path))
  }

  connectButton(): Locator {
    return this.page.getByRole('button', { name: /connect/i })
  }

  async clickConnect(): Promise<void> {
    await this.connectButton().click()
  }
}
