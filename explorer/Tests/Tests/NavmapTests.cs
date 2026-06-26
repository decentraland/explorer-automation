namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Navmap Tests")]
[Category("InWorld")]
[Order(13)]
public class NavmapTests : BaseTest
{
    [Test]
    public void TestClickAllCategoryFilters()
    {
        PressKey(AltKeyCode.M);
        Views.ExplorePanel.Navmap.WaitFor();

        Views.ExplorePanel.Navmap.AllCategoryButton.Click();
        Reporter.Log("Clicked All category filter");

        Views.ExplorePanel.Navmap.FavoritesCategoryButton.Click();
        Reporter.Log("Clicked Favorites category filter");

        Views.ExplorePanel.Navmap.SocialCategoryButton.Click();
        Reporter.Log("Clicked Social category filter");

        Views.ExplorePanel.Navmap.MusicCategoryButton.Click();
        Reporter.Log("Clicked Music category filter");

        Views.ExplorePanel.Navmap.ArtCategoryButton.Click();
        Reporter.Log("Clicked Art category filter");

        Views.ExplorePanel.Navmap.GameCategoryButton.Click();
        Reporter.Log("Clicked Game category filter");

        Views.ExplorePanel.Navmap.FashionCategoryButton.Click();
        Reporter.Log("Clicked Fashion category filter");

        Views.ExplorePanel.Navmap.EducationCategoryButton.Click();
        Reporter.Log("Clicked Education category filter");

        Views.ExplorePanel.Navmap.ShopCategoryButton.Click();
        Reporter.Log("Clicked Shop category filter");

        Views.ExplorePanel.Navmap.SportCategoryButton.Click();
        Reporter.Log("Clicked Sport category filter");

        Views.ExplorePanel.Navmap.BusinessCategoryButton.Click();
        Reporter.Log("Clicked Business category filter");

        Views.ExplorePanel.Close();
    }
}
