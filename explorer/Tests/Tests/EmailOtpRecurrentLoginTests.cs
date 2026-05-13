namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Email + OTP Login")]
[Category("Auth")]
[Order(3)]
[Ignore("Auth suite temporarily disabled")]
public class EmailOtpRecurrentLoginTests : LoggedOutAuthBaseTest
{
    // Precondition: the account at IMAP_USER (no plus-alias) must already exist —
    // i.e. it has previously completed the new-user setup screen at least once. If the
    // account is fresh, this test will fail because the WelcomeNewAccountScreen will appear
    // instead of going straight in-world.

    [Test]
    public void TestRecurrentUserCanLoginWithEmailOtp()
    {
        var email = OtpMailbox.GetBaseEmail();
        Reporter.Log($"Recurrent-user email: {email}");

        // Step 1 — submit the registered email. No retry on a different email: the
        // recurrent flow MUST use the actual base account, so a different fresh address
        // would just sign up a new account.
        SubmitEmailWithRetry(() => email, maxAttempts: 1);

        // Step 2 — fetch OTP from inbox and submit
        var code = OtpMailbox.WaitForOtp(email);
        Views.OtpVerificationScreen.OtpInput.SetText(code);
        Reporter.Log("OTP submitted; recurrent user should land on cached-account screen, not new-user setup");

        // Step 3 — recurrent user lands on the cached-account screen ("Welcome <name> /
        // Ready to explore?" with a JumpIntoWorld button), not on WelcomeNewAccountScreen.
        Views.AuthenticationMainScreen.JumpIntoWorldButton.WaitFor(60);
        Assert.That(Views.WelcomeNewAccountScreen.IsPresent(), Is.False,
            "Welcome / new-user setup screen should not appear for a recurrent user");

        // Step 4 — click JUMP INTO DECENTRALAND
        Views.AuthenticationMainScreen.JumpIntoWorldButton.Click();
        Reporter.Log("Clicked Jump Into Decentraland — waiting for in-world");

        // Step 5 — wait for in-world by polling the backpack shortcut.
        // Recurrent flow tends to take longer to wire input than new-user, hence the higher
        // timeout here than in the new-user test.
        WaitForInWorld(timeoutSeconds: 360);
        Reporter.Log("Player is in-world (backpack shortcut responsive)");

        // Step 6 — backpack already open from the in-world poll. Verify and close.
        Assert.That(Views.ExplorePanel.Backpack.IsPresent(), Is.True,
            "Backpack section should be visible after pressing I");
        PressEscape();
        Views.ExplorePanel.WaitForGone();

        // Step 7 — jump.
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
    /// between builds and the post-OTP loading state varies.
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
