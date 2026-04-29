namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the email OTP verification screen shown after submitting an email
/// on the authentication main screen. The six visible digit boxes are display-only;
/// the input is a single hidden TMP_InputField that accepts the full 6-digit code.
/// </summary>
public class OtpVerificationScreenView() :
    BaseView(new(By.NAME, "Verification.OTP.Screen"))
{
    #region Elements

    public readonly Writable  OtpInput          = new(By.PATH, "//TextInput.OTP/Hidden.InputField");
    public readonly Readable  DescriptionText   = new(By.PATH, "//Verification.OTP.Screen/Description");
    public readonly Readable  TitleText         = new(By.PATH, "//Verification.OTP.Screen/Title.Text");
    public readonly Clickable ResendCodeButton  = new(By.NAME, "RecendCode.Button");
    public readonly Clickable BackButton        = new(By.NAME, "Back.Button");

    #endregion
}
