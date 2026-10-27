using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// F7-05: la pantalla de ciudad debe cargar como árbol UI Toolkit y exponer el
/// inspector y los hotspots del nuevo hub (no solo botones de navegación).
/// </summary>
public class CityUiTests
{
    const string UxmlPath = "Assets/_TacticalFive/UI/Screens/TheCity/TheCity.uxml";
    const string UssPath = "Assets/_TacticalFive/UI/Screens/TheCity/TheCity.uss";

    [Test]
    public void TheCityUxml_LoadsAndContainsTheHubElements()
    {
        var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        Assert.That(asset, Is.Not.Null, $"No se pudo cargar {UxmlPath}");

        var root = asset.CloneTree();
        Assert.That(root.Q<Label>("CityPulseBudget"), Is.Not.Null);
        Assert.That(root.Q<Label>("CityPulseFan"), Is.Not.Null);
        Assert.That(root.Q<Label>("CityPulseObjective"), Is.Not.Null);
        Assert.That(root.Q<Label>("CityPulseNextGame"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("CityInspectorBody"), Is.Not.Null);
        Assert.That(root.Q<Button>("CityTabBuilding"), Is.Not.Null);
        Assert.That(root.Q<Button>("CityTabProjects"), Is.Not.Null);
        Assert.That(root.Q<Button>("CityTabEvents"), Is.Not.Null);

        foreach (string hotspot in new[]
                 {
                     "CityHospital", "CityArena", "CityTraining", "CityLeague",
                     "CityPersonnel", "CityBank", "CityNewspaper", "CityStatistics", "CityMuseum"
                 })
        {
            Assert.That(root.Q<Button>(hotspot), Is.Not.Null, $"Falta el hotspot {hotspot}");
        }
    }

    [Test]
    public void TheCityStylesheet_Loads()
    {
        var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
        Assert.That(style, Is.Not.Null, $"No se pudo cargar {UssPath}");
    }
}
