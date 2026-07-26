using UnityEngine;
using UnityEngine.UIElements;

public class LoadingController : MonoBehaviour
{
    private static readonly string[] Tips =
    {
        "El baloncesto no se juega con las manos, se juega con el coraz\u00f3n.",
        "Un buen entrenador gana partidos, uno grande gana campeonatos.",
        "La clave del \u00e9xito est\u00e1 en la preparaci\u00f3n y el scouting.",
        "No hay 'yo' en 'equipo' \u2014 la qu\u00edmica lo es todo.",
        "El trabajo duro vence al talento cuando el talento no trabaja duro.",
        "Defensa gana campeonatos, ataque gana partidos.",
        "Cada partido es una oportunidad para escribir tu leyenda.",
        "Gestiona bien tu presupuesto: el mercado nunca cierra.",
        "Un draft acertado puede cambiar el futuro de tu franquicia.",
        "La afici\u00f3n nunca olvida a los que dan todo en la pista."
    };

    private UIDocument _doc;
    private VisualElement _root;
    private Label _tipLabel;
    private bool _transitioning;

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;
        _root.style.flexGrow = 1f;

        _tipLabel = _root.Q<Label>("LoadingTip");
        if (_tipLabel != null)
            _tipLabel.text = Tips[Random.Range(0, Tips.Length)];

        _root.RegisterCallback<ClickEvent>(_ => OnInput());
        _root.RegisterCallback<KeyDownEvent>(_ => OnInput());
        _root.focusable = true;
        _root.Focus();

        StartCoroutine(LoadingTimer());
    }

    System.Collections.IEnumerator LoadingTimer()
    {
        yield return new WaitForSeconds(10f);
        OnInput();
    }

    void OnInput()
    {
        if (_transitioning) return;
        _transitioning = true;
        ScreenManager.Instance.GoTo(GameScreen.MainMenu);
    }
}
