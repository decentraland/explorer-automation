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

    private static readonly Random Rng = new();

    /// <summary>
    /// Submits an email and waits for the OTP screen. Builds a pool with the primary plus
    /// the addresses configured in <c>ALTERNATE_EMAILS</c> (each freshened with a
    /// new <c>+hash</c>). When <paramref name="shufflePool"/> is true the pool is randomized
    /// to spread load across Thirdweb's per-address rate-limit buckets — recommended for
    /// new-user signup tests. Recurrent tests should pass <c>false</c> (and a primary that
    /// is the actual existing account) so we don't sign up a different account by accident.
    /// On 429 (OTP screen never appears), the helper moves to the next candidate.
    /// Returns the email that actually got the OTP screen.
    /// </summary>
    /// <param name="primaryEmail">The email to try first (or one of, when shuffling).</param>
    /// <param name="otpScreenTimeoutSec">How long to wait for the OTP screen per attempt.</param>
    /// <param name="shufflePool">Randomize the order of candidates.</param>
    protected string SubmitEmailWithRateLimitFallback(string primaryEmail, int otpScreenTimeoutSec = 25, bool shufflePool = false)
    {
        var candidates = new List<string> { primaryEmail };
        candidates.AddRange(OtpMailbox.GetAlternateEmails()
                                      .Select(OtpMailbox.GeneratePlusAliasEmail));

        if (shufflePool)
        {
            // Fisher-Yates shuffle.
            for (var i = candidates.Count - 1; i > 0; i--)
            {
                var j = Rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            Reporter.Log($"Pool shuffled — first try: {candidates[0]}");
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var email = candidates[i];
            Reporter.Log($"Submitting email ({i + 1}/{candidates.Count}): {email}");
            Views.AuthenticationMainScreen.EmailInput.SetText(email);

            try
            {
                Views.OtpVerificationScreen.WaitFor(otpScreenTimeoutSec);
                Reporter.Log($"OTP screen appeared for {email}");
                return email;
            }
            catch (AssertionException)
            {
                Reporter.Log($"OTP screen did not appear for {email} within {otpScreenTimeoutSec}s — likely rate-limited; trying next candidate");
                Reporter.TakeScreenshot($"RateLimit_{email.Replace('@', '_').Replace('+', '_')}");

                // The Thirdweb error popup blocks input — Escape closes it (or its EXIT button).
                PressEscape();
                Wait(1);

                // Make sure we're back at LoginSelection before trying the next email.
                if (!Views.AuthenticationMainScreen.LoginSelectionScreen.IsPresent())
                {
                    PressEscape();
                    Wait(1);
                }
            }
        }

        throw new AssertionException(
            $"All {candidates.Count} email candidates failed to bring up the OTP screen. " +
            "Set ALTERNATE_EMAILS to a comma-separated list of fallback addresses, " +
            "or wait for the Thirdweb rate-limit window to clear.");
    }
}
