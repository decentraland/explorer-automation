import type { Page } from '@playwright/test';

/**
 * Page Object for the Decentraland landing page (`https://decentraland.org`).
 * Logged-out visitors see a "Sign In" button that takes them to `/auth`.
 */
export class LandingPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto('/');
  }

  async clickSignIn(): Promise<void> {
    await this.page.getByRole('button', { name: 'Sign In' }).click();
  }
}
