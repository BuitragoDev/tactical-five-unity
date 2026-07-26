using UnityEngine;
using UnityEngine.UIElements;

public class LoadingController : MonoBehaviour
{
    private static readonly string[] Tips =
    {
        "He fallado más de 9.000 tiros en mi carrera. He perdido casi 300 partidos. 26 veces me han confiado el tiro ganador y he fallado. He fallado una y otra vez en mi vida. Y por eso tengo éxito. — Michael Jordan",
        "El talento gana partidos, pero el trabajo en equipo y la inteligencia ganan campeonatos. — Michael Jordan",
        "No puedes poner un límite a nada. Cuanto más sueñas, más lejos llegas. — Michael Phelps",
        "El baloncesto es mi esposa. Exige lealtad y responsabilidad, y me devuelve satisfacción y paz. — Jerry West",
        "El éxito no es un accidente. Es trabajo duro, perseverancia, aprendizaje, estudio, sacrificio y, sobre todo, amor por lo que estás haciendo. — Pelé",
        "La fuerza del equipo está en cada miembro individual. La fuerza de cada miembro está en el equipo. — Phil Jackson",
        "La excelencia es el resultado gradual de esforzarse siempre por hacerlo mejor. — Pat Riley",
        "Todo lo negativo —presión, desafíos— es una oportunidad para que me supere. — Kobe Bryant",
        "No importa lo bueno que seas, siempre puedes mejorar. — LeBron James",
        "El baloncesto es un juego de errores. Quien cometa menos errores gana. — John Wooden"
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
