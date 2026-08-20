using UnityEngine;

public class TheCityController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.TheCity;

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[TheCity] RefreshHeader error: {ex.Message}"); }
    }
}