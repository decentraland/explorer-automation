namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Email + OTP Login")]
[Category("Auth")]
// MUST stay last — see comment on EmailOtpLoginTests.
[Order(1001)]
public class EmailOtpLoginWithNewsletterTests : LoggedOutAuthBaseTest
{
    [Test]
    public void TestNewUserCanLoginWithEmailOtpAndSubscribeToNewsletter()
    {
        // Step 1 — submit email. Each call to GenerateFreshEmail returns a brand-new
        // qa-<hash>@<EMAIL_DOMAIN> recipient (its own per-address rate-limit bucket),
        // so we can retry transparently on any transient failure.
        var email = SubmitEmailWithRetry(OtpMailbox.GenerateFreshEmail);

        // Step 2 — fetch OTP from inbox and submit
        var code = OtpMailbox.WaitForOtp(email);
        Views.OtpVerificationScreen.OtpInput.SetText(code);

        // Step 3 — new-user setup with newsletter opt-in. Insert short waits between actions:
        // the screen animates in and the form-validity logic (which gates the JumpIn button)
        // reacts a beat after each interaction, so back-to-back clicks can land on a
        // non-yet-enabled control.
        Views.WelcomeNewAccountScreen.WaitFor(60);
        Wait(1);
        var username = "QA" + Guid.NewGuid().ToString("N")[..6];
        Views.WelcomeNewAccountScreen.UsernameInput.SetText(username);
        Wait(1);
        Views.WelcomeNewAccountScreen.SubscribeToggle.Click();
        Wait(1);
        Views.WelcomeNewAccountScreen.TermsOfUseToggle.Click();
        Wait(1);
        Views.WelcomeNewAccountScreen.RandomizeButton.Click();
        Wait(1);
        Views.WelcomeNewAccountScreen.JumpInButton.Click();
        Reporter.Log($"New account created with username '{username}', subscribed to newsletter, avatar randomized, jumping in");

        // Step 4 — wait for in-world by polling the backpack shortcut.
        WaitForInWorld(timeoutSeconds: 240);
        Reporter.Log("Player is in-world (backpack shortcut responsive)");

        // Step 5 — backpack already open from the in-world poll. Verify and close.
        Assert.That(Views.ExplorePanel.Backpack.IsPresent(), Is.True,
            "Backpack section should be visible after pressing I");
        PressEscape();
        Views.ExplorePanel.WaitForGone();

        // Step 6 — jump.
        PressKey(AltKeyCode.Space);
        Wait(1);
        Reporter.Log("Jump issued");
    }

    /// <summary>
    /// Polls until the in-world Explore panel responds to the backpack shortcut.
    /// </summary>
    /// <remarks>
    /// Uses the actual interaction outcome (pressing I opens backpack) as the in-world signal,
    /// which is more robust than waiting on a specific HUD GameObject — sidebar UUIDs rotate
    /// between builds and the post-Jump-In loading state varies for new users.
    /// We give the client a generous initial grace period because new-user onboarding has
    /// avatar-creation, world streaming, and tutorial overlays before input is wired up.
    /// </remarks>
    private void WaitForInWorld(int timeoutSeconds)
    {
        const int INITIAL_GRACE_SECONDS = 30;
        Reporter.Log($"Waiting {INITIAL_GRACE_SECONDS}s for world to load before probing backpack shortcut");
        Thread.Sleep(INITIAL_GRACE_SECONDS * 1000);

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds - INITIAL_GRACE_SECONDS);
        while (DateTime.UtcNow < deadline)
        {
            PressKey(AltKeyCode.I);
            Thread.Sleep(2000);
            if (Views.ExplorePanel.IsPresent())
                return;
            Reporter.Log("Backpack not open yet — still loading; retrying in 5s");
            Thread.Sleep(5000);
        }
        throw new AssertionException(
            $"Player did not reach in-world state within {timeoutSeconds}s (backpack shortcut never opened the explore panel).");
    }
}
