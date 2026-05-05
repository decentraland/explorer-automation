import type { Locator, Page } from '@playwright/test';

/**
 * Page Object for `https://decentraland.org/avatar-setup` — the WebGL avatar
 * editor shown to new users when the dapp detects a working WebGPU/WebGL
 * stack. The whole UI inside `#avatar-preview-configurator` is a Unity canvas
 * (no DOM elements to query), so we drive it via `page.mouse.click` at
 * relative coordinates inside the iframe's bounding box.
 *
 * Requires the `webgpu` Playwright project (1200x997 viewport + SwiftShader
 * launch flags) — the grid-cell coordinates below are calibrated for that
 * viewport.
 *
 * Ported with minimal changes from `auth-e2e-tests/test/e2e/pages/AvatarSetupPage.ts`.
 */

async function clickInsideElement(
  page: Page,
  locator: Locator,
  xPercent: number,
  yPercent: number,
): Promise<void> {
  const box = await locator.boundingBox();
  if (!box) throw new Error('Element bounding box not found');
  await page.mouse.click(box.x + box.width * xPercent, box.y + box.height * yPercent);
}

// Grid layout constants (relative to iframe bounding box at 1200x997 viewport).
// The item grid starts at (GRID_X_START, GRID_Y_START) with items spaced
// GRID_DX horizontally and GRID_DY vertically, 6 columns per row.
const GRID_X_START = 0.17;
const GRID_Y_START = 0.3;
const GRID_DX = 0.09;
const GRID_DY = 0.12;
const GRID_COLS = 6;

async function selectRandomGridItem(
  page: Page,
  locator: Locator,
  rows: number,
  cols: number = GRID_COLS,
): Promise<void> {
  const col = Math.floor(Math.random() * cols);
  const row = Math.floor(Math.random() * rows);
  await clickInsideElement(
    page,
    locator,
    GRID_X_START + col * GRID_DX,
    GRID_Y_START + row * GRID_DY,
  );
}

// Face categories: sidebar Y position + available grid rows.
// `rows` is conservative on categories with partial last rows to avoid
// clicking dead space.
const FACE_CATEGORIES = [
  { sidebarY: 0.28, rows: 4 }, // Hair
  { sidebarY: 0.36, rows: 4 }, // Eyes
  { sidebarY: 0.43, rows: 4 }, // Eyebrows
  { sidebarY: 0.5, rows: 4 }, // Mouth
  { sidebarY: 0.57, rows: 2 }, // Facial Hair
];

const OUTFIT_CATEGORIES = [
  { sidebarY: 0.28, rows: 3 }, // Upper Body
  { sidebarY: 0.36, rows: 3 }, // Lower Body
  { sidebarY: 0.43, rows: 4 }, // Feet
  { sidebarY: 0.5, rows: 3 }, // Eyewear
  { sidebarY: 0.57, rows: 1 }, // Handwear
  { sidebarY: 0.63, rows: 2 }, // Earrings
];

export class AvatarSetupPage {
  constructor(private readonly page: Page) {}

  async waitFor(timeoutMs = 60_000): Promise<void> {
    // The route is `/auth/avatar-setup` (dapp basename is `/auth`). Match
    // loosely so it works whether or not the dapp adds a redirectTo query.
    await this.page.waitForURL(/\/auth\/avatar-setup/, { timeout: timeoutMs });
    await this.page
      .locator('input[placeholder="Enter your username"]')
      .waitFor({ state: 'visible', timeout: timeoutMs });
  }

  /**
   * Fills the profile form (username, optional email, terms) and clicks
   * "MEET MY AVATAR" to enter the customization editor.
   * The button is disabled until the WearablePreview iframe finishes loading
   * the WebGL scene; the visible-wait below covers that.
   */
  async completeProfile(username: string, email?: string): Promise<void> {
    const usernameInput = this.page.locator('input[placeholder="Enter your username"]');
    await usernameInput.waitFor({ state: 'visible', timeout: 30_000 });
    await usernameInput.fill(username);

    if (email) {
      const emailInput = this.page.locator('input[placeholder="Enter your email"]');
      await emailInput.waitFor({ state: 'visible', timeout: 10_000 });
      await emailInput.fill(email);
    }

    const termsCheckbox = this.page.locator('#terms');
    await termsCheckbox.waitFor({ state: 'visible', timeout: 10_000 });
    await termsCheckbox.check();

    // The iframe overlay intercepts pointer events while loading; force-click.
    const continueButton = this.page.locator('[data-testid="avatar-setup-continue-button"]');
    await continueButton.waitFor({ state: 'visible', timeout: 60_000 });
    await continueButton.click({ force: true });
  }

  /**
   * Selects the first preset avatar and clicks "CUSTOMIZE LATER" — the
   * minimal exit path through the editor.
   */
  async skipAvatarCustomization(): Promise<void> {
    const wearablePreview = this.page.locator('#avatar-preview-configurator');
    await wearablePreview.waitFor({ state: 'visible', timeout: 30_000 });
    // Unity needs a generous warm-up; "visible" fires before the Unity scene
    // is interactive. 15s is a safe ceiling — running on a real GPU it's
    // usually ready in 6-8s, but this avoids brittle false-failure when the
    // GPU is contended.
    await this.page.waitForTimeout(15_000);

    // Coords calibrated against current prod via OCR on a 1200x997 capture.
    // Buttons sit around y=766; START CUSTOMIZATION centered at x=434, and
    // CUSTOMIZE LATER at x=627.

    // Top-left preset
    await clickInsideElement(this.page, wearablePreview, 0.05, 0.32);
    await this.page.waitForTimeout(3_000);

    // "CUSTOMIZE LATER >"
    await clickInsideElement(this.page, wearablePreview, 0.52, 0.77);
    await this.page.waitForTimeout(2_000);
  }

  /**
   * Drives the full editor: pick a starting preset → start customization →
   * cycle through every face category and pick a random item → cycle through
   * every outfit category and pick a random item → finish. Mirrors what a
   * user would do clicking around the Unity UI; meant as a regression guard
   * for the editor + wearable-loading pipeline.
   */
  async completeAvatarCustomization(): Promise<void> {
    const wearablePreview = this.page.locator('#avatar-preview-configurator');
    await wearablePreview.waitFor({ state: 'visible', timeout: 30_000 });
    await this.page.waitForTimeout(8_000);

    // Step 1 — pick starting look
    // (preset grid + bottom buttons coords calibrated via OCR on prod)
    await clickInsideElement(this.page, wearablePreview, 0.05, 0.32);
    await this.page.waitForTimeout(2_000);
    // "START CUSTOMIZATION"
    await clickInsideElement(this.page, wearablePreview, 0.36, 0.77);
    await this.page.waitForTimeout(5_000);

    // Step 2 — face
    for (const cat of FACE_CATEGORIES) {
      await clickInsideElement(this.page, wearablePreview, 0.07, cat.sidebarY);
      await this.page.waitForTimeout(1_000);
      await selectRandomGridItem(this.page, wearablePreview, cat.rows);
      await this.page.waitForTimeout(2_000);
    }
    // "CUSTOMIZE OUTFIT >"
    await clickInsideElement(this.page, wearablePreview, 0.49, 0.77);
    await this.page.waitForTimeout(3_000);

    // Step 3 — outfit
    for (const cat of OUTFIT_CATEGORIES) {
      await clickInsideElement(this.page, wearablePreview, 0.07, cat.sidebarY);
      await this.page.waitForTimeout(1_000);
      await selectRandomGridItem(this.page, wearablePreview, cat.rows);
      await this.page.waitForTimeout(2_000);
    }
    // Wait for the last wearable to finish loading before FINISH; the
    // submit-to-catalyst step needs the avatar metadata fully committed.
    await this.page.waitForTimeout(10_000);
    // "FINISH" (OCR-calibrated: text center x=671, y=766 on 1200x997 → 0.56, 0.77)
    await clickInsideElement(this.page, wearablePreview, 0.56, 0.77);
    await this.page.waitForTimeout(2_000);
  }
}
