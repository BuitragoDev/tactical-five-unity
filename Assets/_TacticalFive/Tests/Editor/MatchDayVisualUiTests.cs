using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

public class MatchDayVisualUiTests
{
    static VisualElement Load(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        Assert.That(asset, Is.Not.Null, $"No se pudo cargar {path}");
        return asset.CloneTree();
    }

    [Test]
    public void MatchDay_HasVisualTwoDOverlay()
    {
        var root = Load("Assets/_TacticalFive/UI/Screens/MatchDay/MatchDay.uxml");

        Assert.That(root.Q<VisualElement>("SimVisualOverlay"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("Sim2DCourt"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("Sim2DHomeLayer"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("Sim2DAwayLayer"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("Sim2DBall"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("Sim2DTeamList"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("Sim2DLeftHoopLogo"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("Sim2DRightHoopLogo"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("Sim2DLeftAttackArrow"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("Sim2DRightAttackArrow"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("Sim2DEffectsLayer"), Is.Not.Null);
        Assert.That(root.Q<Label>("Sim2DScore"), Is.Not.Null);
        Assert.That(root.Q<Label>("Sim2DTickerText"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("Sim2DTickerIcon"), Is.Not.Null);
        Assert.That(root.Q<Button>("Sim2DHomeTab"), Is.Not.Null);
        Assert.That(root.Q<Button>("Sim2DAwayTab"), Is.Not.Null);
        Assert.That(root.Q<Button>("Sim2DSpeed1"), Is.Not.Null,
            "Debe existir la velocidad real x1");
        Assert.That(root.Q<Button>("Sim2DSpeed5"), Is.Not.Null);
        Assert.That(root.Q<Button>("Sim2DSpeed10"), Is.Not.Null);
        Assert.That(root.Q<Button>("Sim2DSpeed20"), Is.Not.Null);
        Assert.That(root.Q<Button>("Sim2DSpeed1").text,
            Is.EqualTo("{loc:ui.match_day.pbp_btn_speed1}"));
        Assert.That(root.Q<Button>("Sim2DSkip"), Is.Not.Null);

        LocalizationService.Apply(root);
        Assert.That(root.Q<Label>("Sim2DHeaderPlayer").text, Is.EqualTo("Jugador"));
        Assert.That(root.Q<Label>("Sim2DHeaderPts").text, Is.EqualTo("PTS"));
        Assert.That(root.Q<Label>("Sim2DHeaderReb").text, Is.EqualTo("REB"));
        Assert.That(root.Q<Label>("Sim2DHeaderAsi").text, Is.EqualTo("ASI"));
    }

    [Test]
    public void ConfigModals_HaveVisualTwoDButton()
    {
        var dashboard = Load("Assets/_TacticalFive/UI/Screens/Dashboard/Dashboard.uxml");
        var mainMenu = Load("Assets/_TacticalFive/UI/Screens/MainMenu/MainMenu.uxml");

        Assert.That(dashboard.Q<Button>("ConfigBtnSimVisual"), Is.Not.Null);
        Assert.That(mainMenu.Q<Button>("ConfigBtnSimVisual"), Is.Not.Null);
        Assert.That(dashboard.Q<Button>("ConfigBtnSimVisual").text, Is.EqualTo("{loc:ui.dashboard.config_btn_sim_visual}"));
        Assert.That(mainMenu.Q<Button>("ConfigBtnSimVisual").text, Is.EqualTo("{loc:ui.main_menu.config_btn_sim_visual}"));
    }
}
