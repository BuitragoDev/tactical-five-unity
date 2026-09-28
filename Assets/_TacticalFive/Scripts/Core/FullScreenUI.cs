using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class FullScreenUI : MonoBehaviour
{
    void Awake()
    {
        var doc = GetComponent<UIDocument>();
        // En Awake el UIDocument todavía no ha creado su árbol (se hace en OnEnable);
        // la base UIScreenController ya aplica el fullscreen, así que aquí solo se
        // protege el acceso para no ensuciar la consola con un NRE.
        var root = doc != null ? doc.rootVisualElement : null;
        if (root == null) return;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.right = 0;
        root.style.top = 0;
        root.style.bottom = 0;
        root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
    }
}