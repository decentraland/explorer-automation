namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Email + OTP Login")]
[Category("Auth")]
// MUST stay last — see comment on EmailOtpLoginTests.
[Order(1002)]
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

        // Step 5 — wait for in-world via the LoadingScreen + SidebarView signal.
        WaitForInWorldAfterJumpIn();

        // Step 6 — open backpack via the shortcut and verify.
        OpenExplorePanelViaShortcut();
        Assert.That(Views.ExplorePanel.Backpack.IsPresent(), Is.True,
            "Backpack section should be visible after pressing I");
        PressEscape();
        Views.ExplorePanel.WaitForGone();

        // Step 7 — jump.
        PressKey(AltKeyCode.Space);
        Wait(1);
        Reporter.Log("Jump issued");
    }
}
