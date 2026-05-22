namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Base fixture for tests that exercise the logged-out auth flow (email + OTP, etc.).
/// Reuses the standard <see cref="BaseTest"/> lifecycle but replaces the in-world ensure
/// step with an "ensure logged-out" flow: no matter what state the Explorer is in when
/// the fixture starts (in-world from a previous test, cached-account screen, or already
/// logged-out), this navigates the UI down to the LoginSelection screen so each fixture
/// can drive a fresh email+OTP login. Without this, multiple logged-out fixtures running
/// in the same Explorer session would step on each other and CI couldn't run them all.
/// </summary>
public abstract class LoggedOutAuthBaseTest : BaseTest
{
    protected override void EnsureInWorld()
    {
        Views.SplashScreen.WaitForGone();

        // Case 1: already at the auth screen.
        if (Views.AuthenticationMainScreen.IsPresent())
        {
            EnsureLoggedOutFromAuthScreen();
            return;
        }

        // Case 2: already in-world (previous test left us here). Sign out via profile menu.
        Reporter.Log("In-world detected — opening profile menu to sign out");
        Views.MainMenu.ProfileButton.Click();
        var profileMenu = Views.ProfileMenu.WaitFor();

        // ViewBase.ShowAsync (in unity-explorer's MVC) disables the GraphicRaycaster on
        // the root while the open animation plays, then re-enables it. WaitFor only checks
        // GameObject existence, so without this guard our click can land on a modal whose
        // raycaster eats the event. Wait for the raycaster to come back on.
        profileMenu.WaitForComponentProperty(
            "UnityEngine.UI.GraphicRaycaster",
            "enabled",
            true,
            "UnityEngine.UI",
            timeout: 15);

        Views.ProfileMenu.SignOutButton.Click();
        Reporter.Log("Sign Out clicked — waiting for auth flow");

        // Sign-out drops us back through the splash → auth pipeline.
        Views.SplashScreen.WaitForGone(60);
        Views.AuthenticationMainScreen.WaitFor(60);
        EnsureLoggedOutFromAuthScreen();
    }

    /// <summary>
    /// When the auth screen is showing, ensure the LoginSelection (logged-out) sub-screen
    /// is visible. Handles three sub-states:
    /// - LoginSelection already visible → done
    /// - Cached-account variant → click "Use a Different Account"
    /// - Stuck on OTP verification (e.g. previous test left it there) → click Back
    /// </summary>
    private void EnsureLoggedOutFromAuthScreen()
    {
        if (Views.AuthenticationMainScreen.LoginSelectionScreen.IsPresent())
        {
            Reporter.Log("Already at logged-out auth screen — ready");
            return;
        }

        if (Views.OtpVerificationScreen.IsPresent())
        {
            Reporter.Log("Stuck on OTP verification screen — pressing Back to return to login selection");
            Views.OtpVerificationScreen.BackButton.Click();
            Views.AuthenticationMainScreen.LoginSelectionScreen.WaitFor(30);
            Reporter.Log("Returned to logged-out auth screen");
            return;
        }

        Reporter.Log("Cached-account screen present — clicking 'Use a Different Account'");
        Views.AuthenticationMainScreen.UseADifferentAccountButton.Click();
        Views.AuthenticationMainScreen.LoginSelectionScreen.WaitFor(30);
        Reporter.Log("Reached logged-out auth screen");
    }

    /// <summary>
    /// Submits an email and waits for the OTP screen. On failure (transient error or rare
    /// per-address rate limit), regenerates the email via <paramref name="emailFactory"/>
    /// and retries up to <paramref name="maxAttempts"/>. Returns the email that succeeded.
    ///
    /// New-user tests should pass <see cref="OtpMailbox.GenerateFreshEmail"/> as the
    /// factory so each attempt is a brand-new recipient (its own rate-limit bucket).
    /// Recurrent-login tests should pass a closure that returns the registered
    /// <c>IMAP_USER</c> and use <c>maxAttempts=1</c> — re-submitting the same registered
    /// account doesn't help.
    /// </summary>
    /// <param name="emailFactory">Returns the email to submit on each attempt.</param>
    /// <param name="otpScreenTimeoutSec">How long to wait for the OTP screen per attempt.</param>
    /// <param name="maxAttempts">Maximum number of attempts before failing.</param>
    /// <summary>
    /// Wait for the world to fully load after a JumpIn click (new-user WelcomeNewAccountScreen
    /// or recurrent-user AuthenticationMainScreen). Mirrors <c>BaseTest.EnsureInWorld</c>'s
    /// post-JumpIn pattern: poll LoadingScreen → wait for SidebarView → settle for shortcut
    /// listener subscription.
    /// </summary>
    /// <remarks>
    /// The original implementation polled <c>PressKey(I)</c> until ExplorePanel appeared, but
    /// while LoadingScreen is up the SidebarController has not wired its OnClick listeners yet
    /// and shortcut presses are silently dropped — so the poll can never succeed within its
    /// budget on slow runners. On macos-14 paravirt the new-user world stream regularly runs
    /// 4+ minutes (asset bundle warmup + avatar creation + first realm comms), so the loading
    /// budget here is 360s — well past BaseTest's 300s default — to absorb the worst case.
    /// </remarks>
    protected void WaitForInWorldAfterJumpIn()
    {
        try
        {
            Views.LoadingScreen.WaitFor(15);
            Reporter.Log("Scene loading screen visible — waiting for world streaming to finish (up to 6 min)");
            Views.LoadingScreen.WaitForGone(360);
            Reporter.Log("Scene loading complete — HUD should now be interactable");
        }
        catch (Exception)
        {
            Reporter.Log("Scene loading screen never appeared — assuming world was already loaded");
        }

        Views.MainMenu.WaitFor(240);

        // SidebarController subscribes its onClick listeners asynchronously after SidebarView
        // appears; the first shortcut press immediately after can land in that gap and get
        // dropped. Same ~20s settle as BaseTest.EnsureInWorld.
        Thread.Sleep(20_000);
        Reporter.Log("Player is in-world and main menu is ready");
    }

    protected string SubmitEmailWithRetry(Func<string> emailFactory, int otpScreenTimeoutSec = 25, int maxAttempts = 3)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            var email = emailFactory();
            Reporter.Log($"Submitting email ({i + 1}/{maxAttempts}): {email}");
            Views.AuthenticationMainScreen.EmailInput.SetText(email);

            try
            {
                Views.OtpVerificationScreen.WaitFor(otpScreenTimeoutSec);
                Reporter.Log($"OTP screen appeared for {email}");
                return email;
            }
            catch (AssertionException)
            {
                Reporter.Log($"OTP screen did not appear for {email} within {otpScreenTimeoutSec}s");
                Reporter.TakeScreenshot($"OtpScreenMissing_{email.Replace('@', '_').Replace('+', '_')}");

                // The Thirdweb error popup blocks input — Escape closes it (or its EXIT button).
                PressEscape();
                Wait(1);

                // Make sure we're back at LoginSelection before trying again.
                if (!Views.AuthenticationMainScreen.LoginSelectionScreen.IsPresent())
                {
                    PressEscape();
                    Wait(1);
                }
            }
        }

        throw new AssertionException(
            $"All {maxAttempts} email attempts failed to bring up the OTP screen.");
    }
}
