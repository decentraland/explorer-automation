namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Section view for the Gallery tab within the explore panel, displaying the user's photo gallery.
/// </summary>
public class ExplorePanelGalleryView() : BaseSection(new(By.NAME, "GallerySection"))
{
    // The gallery loads remote photos after the section is shown, which can flicker the panel's
    // raycaster off again; no Show/Hide fires for that flicker, so settle briefly for it.
    internal override AltObject WaitFor(double timeout, bool verificationShot)
    {
        var altObj = base.WaitFor(timeout, verificationShot);
        Thread.Sleep(750);
        return altObj;
    }
}
