import { test, expect } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { LandingPage } from '../pages/LandingPage.js';
import { AuthPage } from '../pages/AuthPage.js';
import { QuickSetupPage } from '../pages/QuickSetupPage.js';
import { HomePage } from '../pages/HomePage.js';
import { generatePlusAliasEmail, getBaseEmail, waitForOtp } from '../helpers/otp-mailbox.js';

/**
 * Mirrors the C# Auth suite (EmailOtpLoginTests, EmailOtpLoginWithNewsletterTests,
 * EmailOtpRecurrentLoginTests) but for the web dapp at decentraland.org.
 *
 * New users go through `/auth/quick-setup` (username + ToS, optional newsletter
 * email) before reaching the homepage. Recurrent users skip that screen.
 */

function uniqueUsername(): string {
  return `QA${randomBytes(3).toString('hex')}`;
}

async function submitEmailAndOtp(landing: LandingPage, auth: AuthPage, email: string): Promise<void> {
  await landing.goto();
  await landing.clickSignIn();
  await auth.submitEmail(email);
  await auth.waitForOtpScreen();
  const code = await waitForOtp(email);
  await auth.enterOtp(code);
}

test.describe('@web email + OTP login', () => {
  test('new user can log in with email + OTP (no newsletter)', async ({ page }) => {
    const email = generatePlusAliasEmail();
    const landing = new LandingPage(page);
    const auth = new AuthPage(page);
    const quickSetup = new QuickSetupPage(page);
    const home = new HomePage(page);

    await submitEmailAndOtp(landing, auth, email);

    await quickSetup.waitFor();
    await quickSetup.fillUsername(uniqueUsername());
    // intentionally NOT calling subscribeToNewsletter — leaves the email field blank.
    await quickSetup.acceptTerms();
    await quickSetup.submit();

    await quickSetup.clickStartExploring();
    await home.waitFor();
    expect(page.url()).not.toMatch(/\/auth/);
  });

  test('new user can log in with email + OTP and subscribe to newsletter', async ({ page }) => {
    const email = generatePlusAliasEmail();
    const landing = new LandingPage(page);
    const auth = new AuthPage(page);
    const quickSetup = new QuickSetupPage(page);
    const home = new HomePage(page);

    await submitEmailAndOtp(landing, auth, email);

    await quickSetup.waitFor();
    await quickSetup.fillUsername(uniqueUsername());
    // Newsletter opt-in: same address used for OTP — every fallback alias also
    // lands in the same Gmail inbox, so subscribing with this address is fine.
    await quickSetup.subscribeToNewsletter(email);
    await quickSetup.acceptTerms();
    await quickSetup.submit();

    await quickSetup.clickStartExploring();
    await home.waitFor();
    expect(page.url()).not.toMatch(/\/auth/);
  });

  test('recurrent user can log in with email + OTP and skip quick-setup', async ({ page }) => {
    // Precondition: EXPLORER_IMAP_USER (no plus-alias) is already a registered
    // Decentraland account. Mirrors the C# EmailOtpRecurrentLoginTests precondition.
    const email = getBaseEmail();
    const landing = new LandingPage(page);
    const auth = new AuthPage(page);
    const home = new HomePage(page);

    await submitEmailAndOtp(landing, auth, email);

    // Recurrent users go straight to the homepage; quick-setup must NOT appear.
    await home.waitFor(60_000);
    expect(page.url()).not.toMatch(/\/auth\/quick-setup/);
    expect(page.url()).not.toMatch(/\/auth\/login/);
  });
});
