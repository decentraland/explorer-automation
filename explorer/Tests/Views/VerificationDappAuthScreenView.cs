namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the dapp (web3 wallet) verification screen shown after the
/// auth-api server validates that someone is on the corresponding web
/// `/auth/requests/<id>` page. Source: `VerificationDappAuthView.cs` in
/// unity-explorer, prefab `Verification.Dapp.Screen.prefab`.
///
/// Note this screen only appears AFTER the browser-side flow reaches the
/// requests page and the auth-api emits a "request-validation-status" event
/// over its SocketIO connection to the client. Until that handshake happens,
/// the auth screen sits in `LoginSelection.Screen` with a "Cancel" spinner.
///
/// Used by the @cross Flow 1 spec for the device-pairing code-match
/// assertion: after the Playwright browser navigates to /auth/requests/<id>
/// and triggers server validation, this view appears with the code, and the
/// spec asserts the same code is rendered on its web page.
/// </summary>
public class VerificationDappAuthScreenView() :
    BaseView(new(By.NAME, "Verification.Dapp.Screen"))
{
    #region Elements

    /// <summary>
    /// TMP_Text displaying the validation code (an integer rendered via
    /// <c>code.text = dataCode.ToString()</c> on the Unity side). Read via
    /// <c>GetText()</c>.
    /// </summary>
    public readonly Readable  CodeText        = new(By.PATH, "//Verification.Dapp.Screen//Code");

    public readonly Clickable BackButton      = new(By.NAME, "Back.Button");

    /// <summary>
    /// The "?" button that toggles the hint container explaining what the code
    /// is. Not load-bearing for tests, but mapped for completeness.
    /// </summary>
    public readonly Clickable CodeInfoButton  = new(By.NAME, "CodeInfoButton");

    #endregion
}
