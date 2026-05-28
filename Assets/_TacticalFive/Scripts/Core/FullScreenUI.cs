using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class FullScreenUI : MonoBehaviour
{
    void Awake()
    {
        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.right = 0;
        root.style.top = 0;
        root.style.bottom = 0;
        root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
    }
}