namespace ExplorerAutomation.Tests.Views;

public class MainMenuView() : BaseView(new(By.ID, "bab6108c-7cce-45a1-9bcd-40412c1f435e"))
{
    public readonly Clickable ProfileButton         = new(By.ID, "578d9b4e-0531-4cb3-abd7-aa79506c1b3e");
    public readonly Clickable NotificationsButton   = new(By.ID, "6c66dc7b-5c51-4b1c-bd27-0814d9c837ae");
    public readonly Clickable EventsButton          = new(By.ID, "d5ac3302-135f-4d89-9af3-56df31776664");
    public readonly Clickable PlacesButton          = new(By.ID, "bcd4b7ed-97f9-419c-8df8-d8a0218388d2");
    public readonly Clickable CommunitiesButton     = new(By.ID, "9335caa1-070d-47cd-92f8-2ab0bee06003");
    public readonly Clickable MapButton             = new(By.ID, "2b8e4546-23be-4e65-973b-7928eb02f238");
    public readonly Clickable BackpackButton        = new(By.ID, "bab6108c-7cce-45a1-9bcd-40412c1f435e");
    public readonly Clickable MarketplaceButton     = new(By.ID, "31e1fb4b-d737-4351-bc21-97e00f715ebe");
    public readonly Clickable GalleryButton         = new(By.ID, "6d5004d7-5a52-4250-b98a-5799f5e8c011");
    public readonly Clickable SettingsButton        = new(By.ID, "e4146db9-0b45-4c41-8cf0-2cde69a0ce0a");
    public readonly Clickable ControlsButton        = new(By.ID, "6f7c9619-29d4-4dfd-8aad-f8b10f56939a");
    public readonly Clickable HelpButton            = new(By.ID, "c02afb7d-0abf-405e-9ecc-48f8cf439f42");
    public readonly Clickable SidebarSettingsButton = new(By.ID, "a7a98fe6-eca1-4f67-996e-2049c9e020bb");
}
