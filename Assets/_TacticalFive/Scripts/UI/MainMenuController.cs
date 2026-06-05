using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnManager;
    private Button _btnProManager;
    private Button _btnLoadGame;
    private Button _btnEditor;
    private Button _btnExit;
    private Button _btnLegal;

    private VisualElement _modalOverlay;
    private VisualElement _modalBox;
    private Button _btnCerrar;

    private VisualElement _exitModalOverlay;
    private VisualElement _exitModalBox;
    private Button _btnExitYes;
    private Button _btnExitNo;

    void OnEnable()
    {
        _doc  = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        // Forzar root a ocupar toda la pantalla
        _root.style.position = Position.Absolute;
        _root.style.left   = 0;
        _root.style.right  = 0;
        _root.style.top    = 0;
        _root.style.bottom = 0;
        _root.style.width  = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        // Botones principales
        _btnManager    = _root.Q<Button>("BtnManager");
        _btnProManager = _root.Q<Button>("BtnProManager");
        _btnLoadGame   = _root.Q<Button>("BtnLoadGame");
        _btnEditor     = _root.Q<Button>("BtnEditor");
        _btnExit       = _root.Q<Button>("BtnExit");
        _btnLegal      = _root.Q<Button>("BtnLegal");

        // Modal Legal
        _modalOverlay = _root.Q<VisualElement>("ModalOverlay");
        _modalBox     = _root.Q<VisualElement>("ModalBox");
        _btnCerrar    = _root.Q<Button>("BtnCerrar");

        // Modal Salir
        _exitModalOverlay = _root.Q<VisualElement>("ExitModalOverlay");
        _exitModalBox     = _root.Q<VisualElement>("ExitModalBox");
        _btnExitYes       = _root.Q<Button>("BtnExitYes");
        _btnExitNo        = _root.Q<Button>("BtnExitNo");

        // Callbacks botones principales
        _btnManager?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnManagerClicked(); });
        _btnProManager?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnProManagerClicked(); });
        _btnLoadGame?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnLoadGameClicked(); });
        _btnEditor?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnEditorClicked(); });
        _btnExit?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnExitClicked(); });
        _btnLegal?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnLegalClicked(); });

        // Callbacks modal legal
        _btnCerrar?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseModal(); });
        _modalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _modalOverlay)
                { PlayClick(); CloseModal(); }
        });

        // Callbacks modal salir
        _btnExitYes?.RegisterCallback<ClickEvent>(_ => { PlayClick(); QuitGame(); });
        _btnExitNo?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseExitModal(); });
        _exitModalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _exitModalOverlay)
                { PlayClick(); CloseExitModal(); }
        });

        // Hover CTA tarjetas
        RegisterCardHover("BtnManager",    "CtaManager");
        RegisterCardHover("BtnProManager", "CtaPro");
        RegisterCardHover("BtnLoadGame",   "CtaLoad");
        RegisterCardHover("BtnEditor",     "CtaEditor");
        RegisterCardHover("BtnExit",       "CtaExit");

        // Cursores
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnManager);
            CursorManager.Instance.RegisterHandCursor(_btnProManager);
            CursorManager.Instance.RegisterHandCursor(_btnLoadGame);
            CursorManager.Instance.RegisterHandCursor(_btnEditor);
            CursorManager.Instance.RegisterHandCursor(_btnExit);
            CursorManager.Instance.RegisterHandCursor(_btnLegal);
            CursorManager.Instance.RegisterHandCursor(_btnCerrar);
            CursorManager.Instance.RegisterHandCursor(_btnExitYes);
            CursorManager.Instance.RegisterHandCursor(_btnExitNo);
        }

        // Escape para cerrar modales
        _root.focusable = true;
        _root.Focus();
        _root.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape)
            {
                if (_exitModalOverlay.style.display == DisplayStyle.Flex)
                    CloseExitModal();
                else if (_modalOverlay.style.display == DisplayStyle.Flex)
                    CloseModal();
            }
        });    
    }

    void RegisterCardHover(string cardName, string ctaName)
    {
        var card = _root.Q<Button>(cardName);
        var cta  = _root.Q<VisualElement>(ctaName);
        if (card == null || cta == null) return;

        cta.style.opacity = 0;

        card.RegisterCallback<MouseEnterEvent>(_ =>
            cta.style.opacity = 1);
        card.RegisterCallback<MouseLeaveEvent>(_ =>
            cta.style.opacity = 0);
    }

    void OpenModal()
    {
        _modalOverlay.style.display = DisplayStyle.Flex;
        _modalBox.style.display     = DisplayStyle.Flex;
    }

    void CloseModal()
    {
        _modalOverlay.style.display = DisplayStyle.None;
        _modalBox.style.display     = DisplayStyle.None;
    }

    void OpenExitModal()
    {
        _exitModalOverlay.style.display = DisplayStyle.Flex;
        _exitModalBox.style.display     = DisplayStyle.Flex;
    }

    void CloseExitModal()
    {
        _exitModalOverlay.style.display = DisplayStyle.None;
        _exitModalBox.style.display     = DisplayStyle.None;
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnManagerClicked()
    {
        int slot = GameSaveManager.FindNextAvailableSlot();
        GameSaveManager.CleanupOrphanDb(slot);
        DatabaseManager.Instance.InitSaveSlot(slot);
        ScreenManager.Instance.GoTo(GameScreen.SelectTeam, GameMode.Manager);
    }

    void OnProManagerClicked()
    {
        int slot = GameSaveManager.FindNextAvailableSlot();
        GameSaveManager.CleanupOrphanDb(slot);
        DatabaseManager.Instance.InitSaveSlot(slot);
        ScreenManager.Instance.GoTo(GameScreen.SelectTeam, GameMode.ProManager);
    }

    void OnLoadGameClicked()
    {
        ScreenManager.Instance.GoTo(GameScreen.LoadGame);
    }

    void OnEditorClicked()
    {
        ScreenManager.Instance.GoTo(GameScreen.SelectTeam, GameMode.Editor);
    }

    void OnExitClicked()
    {
        OpenExitModal();
    }

    void OnLegalClicked()
    {
        OpenModal();
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}