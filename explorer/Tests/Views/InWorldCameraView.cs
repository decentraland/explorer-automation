namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the in-world camera (screencapture) HUD — the fullscreen photo-mode overlay
/// with the framing guides, SHOOT button, gallery shortcut and camera-controls help.
/// Toggled with the C key or the sidebar camera button; the root
/// InWorldCamera.ScreencaptureHUD(Clone) object is only active while photo mode is on.
/// </summary>
public class InWorldCameraView() : BaseView(new(By.NAME, "InWorldCamera.ScreencaptureHUD(Clone)"))
{
    protected override string ViewName => "InWorldCameraView";

    #region Elements

    // NOTE: Button_Close does not respond to AltTester's synthetic click on this build
    // (verified live via UiDump — the HUD stays up). Close photo mode with the C key instead.
    public readonly Clickable CloseButton          = new(By.PATH, "//InWorldCamera.ScreencaptureHUD(Clone)//Button_Close");

    // Opens the camera reel gallery (ExplorePanel CameraReel section). Do NOT click in tests:
    // the gallery reads the local reels folder under ~/Downloads and fires the macOS TCC
    // permission dialog, which steals focus and breaks AltTester (same reason the Gallery
    // sidebar tests are Assert.Ignore'd).
    public readonly Clickable CameraReelButton     = new(By.PATH, "//InWorldCamera.ScreencaptureHUD(Clone)//Button_CameraReel");

    // SHOOT — hidden by the controller when the account's reel storage is full.
    public readonly Clickable TakeScreenshotButton = new(By.PATH, "//InWorldCamera.ScreencaptureHUD(Clone)//Button_TakeScreenshot");
    public readonly Clickable CameraControlsButton = new(By.PATH, "//InWorldCamera.ScreencaptureHUD(Clone)//Button_CameraControls");

    // The captured-photo thumbnail that flies toward the reel icon after a capture.
    // Its GameObject is always active; only its Image component toggles (see WaitForCaptureFx).
    public readonly Locatable CaptureFxImage       = new(By.PATH, "//InWorldCamera.ScreencaptureHUD(Clone)//AnimatedCaptureImage");

    #endregion

    #region Helper methods

    /// <summary>
    /// Closes photo mode with Escape, retrying until the HUD is actually gone. Needed for the
    /// sidebar-button entry point: the freshly shown camera view eats keyboard input for a
    /// while (C and Escape pressed ~1s after the HUD appears are swallowed, while an Escape
    /// pressed a bit later closes it — same eats-input-after-show pattern as ControlsPanel).
    /// </summary>
    [AllureStep("Close photo mode with Escape")]
    public void CloseWithEscape(int attempts = 5)
    {
        for (var i = 0; i < attempts; i++)
        {
            CommonStuff.AltDriver.PressKey(AltKeyCode.Escape);
            Thread.Sleep(3000);

            // Shot-suppressed probe: retry control flow, not a verification.
            if (!IsPresent(verificationShot: false))
            {
                Reporter.Log($"Photo mode closed after {i + 1} Escape press(es)");
                WaitForGone(5);
                return;
            }
        }

        throw new AssertionException($"Photo mode did not close after {attempts} Escape presses");
    }

    /// <summary>
    /// Waits for the screenshot-capture FX to play, which proves a photo was actually taken.
    /// The FX enables the AnimatedCaptureImage's Image component for ~1.7s (white splash +
    /// fly-to-reel transition) once the capture and its metadata are ready, then disables it
    /// again — so this polls the component property rather than object presence.
    /// </summary>
    [AllureStep("Wait for the screenshot capture FX")]
    public void WaitForCaptureFx(double timeout = 10D)
    {
        // Shot-suppressed wait: the verification moment is the FX poll below.
        var fxImage = CaptureFxImage.WaitFor(5D, verificationShot: false);

        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (fxImage.GetComponentProperty<bool>("UnityEngine.UI.Image", "enabled", "UnityEngine.UI"))
            {
                Reporter.TakeVerificationShot("capture_fx_visible");
                Reporter.Log("Screenshot capture FX played — photo captured");
                return;
            }

            Thread.Sleep(100);
        }

        throw new AssertionException($"Screenshot capture FX did not play within {timeout}s — the photo was likely not taken");
    }

    #endregion
}
