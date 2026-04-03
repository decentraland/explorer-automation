namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the place details popup that appears when clicking on a place card in the explore panel.
/// </summary>
public class PlaceDetailView() : BaseView(new(By.NAME, "PlaceDetailPanel(Clone)"))
{
    #region Elements

    public readonly Clickable CloseButton           = new(By.PATH, "//PlaceDetailPanel(Clone)//Button_Close");
    public readonly Locatable PlaceImage            = new(By.PATH, "//PlaceDetailPanel(Clone)//ImageWithSkeletonAnimation/LoadedState/Thumbnail");
    public readonly Readable  PlaceTitle            = new(By.PATH, "//PlaceDetailPanel(Clone)//PlaceTitleText");
    public readonly Readable  CreatorName           = new(By.PATH, "//PlaceDetailPanel(Clone)//CreatorNameText");
    public readonly Readable  UserVisitsText        = new(By.PATH, "//PlaceDetailPanel(Clone)//UserVisitsText");
    public readonly Readable  LikeRateText          = new(By.PATH, "//PlaceDetailPanel(Clone)//LikeRateText");
    public readonly Readable  LivePlayerCount       = new(By.PATH, "//PlaceDetailPanel(Clone)//OnlineCounter/LiveText");
    public readonly Clickable LikeButton            = new(By.PATH, "//PlaceDetailPanel(Clone)//Interactions/Like");
    public readonly Clickable DislikeButton         = new(By.PATH, "//PlaceDetailPanel(Clone)//Interactions/Dislike");
    public readonly Clickable HeartButton           = new(By.PATH, "//PlaceDetailPanel(Clone)//Interactions/Heart");
    public readonly Clickable HomeButton            = new(By.PATH, "//PlaceDetailPanel(Clone)//Interactions/Home");
    public readonly Clickable ShareButton           = new(By.PATH, "//PlaceDetailPanel(Clone)//Interactions/Share");
    public readonly Clickable StartNavigationButton = new(By.PATH, "//PlaceDetailPanel(Clone)//StartNavigationButton");
    public readonly Clickable ExitNavigationButton  = new(By.PATH, "//PlaceDetailPanel(Clone)//ExitNavigationButton");
    public readonly Clickable JumpInButton          = new(By.PATH, "//PlaceDetailPanel(Clone)//JumpInButton");
    public readonly Readable  DescriptionText       = new(By.PATH, "//PlaceDetailPanel(Clone)//DescriptionContainer/Value");
    public readonly Locatable CategoriesContainer   = new(By.PATH, "//PlaceDetailPanel(Clone)//CategoriesContainer/Value");
    public readonly Readable  CoordsText            = new(By.PATH, "//PlaceDetailPanel(Clone)//CoordsContainer/Value/Text");
    public readonly Readable  ParcelsText           = new(By.PATH, "//PlaceDetailPanel(Clone)//ParcelsContainer/Value/Text");
    public readonly Readable  FavoritesText         = new(By.PATH, "//PlaceDetailPanel(Clone)//FavoritesContainer/Text");
    public readonly Readable  UpdatedText           = new(By.PATH, "//PlaceDetailPanel(Clone)//TotalVisitsContainer/Text");

    #endregion
}
