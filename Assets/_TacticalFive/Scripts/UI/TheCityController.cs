using UnityEngine;
using UnityEngine.UIElements;

public class TheCityController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.TheCity;

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();

        BindBubble("CityHospital", GameScreen.Injured);
        BindBubble("CityArena", GameScreen.Arena);
        BindBubble("CityTraining", GameScreen.Training);
        BindBubble("CityLeague", GameScreen.Standings);
        BindBubble("CityPersonnel", GameScreen.Employees);
        BindBubble("CityBank", GameScreen.Finances);
        BindBubble("CityNewspaper", GameScreen.Messages);
        BindBubble("CityStatistics", GameScreen.Stats);
        BindBubble("CityMuseum", GameScreen.Palmares);
    }

    void BindBubble(string buttonName, GameScreen target)
    {
        var button = _root.Q<Button>(buttonName);
        if (button == null) return;
        button.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(target);
        });
        CursorManager.Instance?.RegisterHandCursor(button);
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[TheCity] RefreshHeader error: {ex.Message}"); }
    }
}
