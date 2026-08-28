namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Section view for the Gallery tab within the explore panel, displaying the user's photo gallery.
/// </summary>
public class ExplorePanelGalleryView() : BaseSection(new(By.NAME, "GallerySection"))
{
    // The gallery loads remote photos after the section is shown, and no view-state signal
    // covers that load, so wait for the panel to actually settle before treating it as ready.
    internal override AltObject WaitFor(double timeout, bool verificationShot)
    {
        var altObj = base.WaitFor(timeout, verificationShot: false);
        WaitForPanelInteractive();
        if (verificationShot)
            Reporter.TakeVerificationShot($"appeared_{ShotName}");
        return altObj;
    }
}
