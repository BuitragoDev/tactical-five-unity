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

    private VisualElement _configModalOverlay;
    private VisualElement _configModalBox;
    private Button _btnConfigCerrar;
    private CustomSlider _configSliderMaster;
    private CustomSlider _configSliderMusic;
    private CustomSlider _configSliderSFX;
    private Label _configLabelMaster;
    private Label _configLabelMusic;
    private Label _configLabelSFX;
    private Button _configBtnQualityLow;
    private Button _configBtnQualityMedium;
    private Button _configBtnQualityHigh;
    private Button _configBtnQualityUltra;

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

        CursorManager.Instance?.SetDefaultCursor();
        AudioManager.Instance?.PlayMusic("backgroundMenu");

        InitConfigModal();

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

    void InitConfigModal()
    {
        _configModalOverlay = _root.Q<VisualElement>("ConfigModalOverlay");
        _configModalBox     = _root.Q<VisualElement>("ConfigModalBox");
        _btnConfigCerrar    = _root.Q<Button>("ConfigBtnCerrar");

        _configSliderMaster = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderMaster"),
            _root.Q<VisualElement>("ConfigFillMaster"),
            _root.Q<VisualElement>("ConfigDraggerMaster"));
        _configSliderMusic  = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderMusic"),
            _root.Q<VisualElement>("ConfigFillMusic"),
            _root.Q<VisualElement>("ConfigDraggerMusic"));
        _configSliderSFX    = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderSFX"),
            _root.Q<VisualElement>("ConfigFillSFX"),
            _root.Q<VisualElement>("ConfigDraggerSFX"));
        _configLabelMaster  = _root.Q<Label>("ConfigLabelMaster");
        _configLabelMusic   = _root.Q<Label>("ConfigLabelMusic");
        _configLabelSFX     = _root.Q<Label>("ConfigLabelSFX");
        _configBtnQualityLow    = _root.Q<Button>("ConfigBtnQualityLow");
        _configBtnQualityMedium = _root.Q<Button>("ConfigBtnQualityMedium");
        _configBtnQualityHigh   = _root.Q<Button>("ConfigBtnQualityHigh");
        _configBtnQualityUltra  = _root.Q<Button>("ConfigBtnQualityUltra");

        var configIcon = _root.Q<VisualElement>("ConfigIcon");
        configIcon?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenConfigModal(); });

        _configSliderMaster.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMasterVolume(v);
            UpdateConfigLabels();
        };
        _configSliderMusic.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMusicVolume(v);
            UpdateConfigLabels();
        };
        _configSliderSFX.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetSFXVolume(v);
            UpdateConfigLabels();
        };

        _configBtnQualityLow?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(0); });
        _configBtnQualityMedium?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(1); });
        _configBtnQualityHigh?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(2); });
        _configBtnQualityUltra?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(3); });

        _btnConfigCerrar?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseConfigModal(); });
        _configModalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configModalOverlay)
                { PlayClick(); CloseConfigModal(); }
        });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(configIcon);
            CursorManager.Instance.RegisterHandCursor(_btnConfigCerrar);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityLow);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityMedium);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityHigh);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityUltra);
        }
    }

    void OpenConfigModal()
    {
        CursorManager.Instance?.SetDefaultCursor();
        var am = AudioManager.Instance;
        if (am != null)
        {
            _configSliderMaster.SetValueWithoutNotify(am.MasterVolume);
            _configSliderMusic.SetValueWithoutNotify(am.MusicVolume);
            _configSliderSFX.SetValueWithoutNotify(am.SFXVolume);
            UpdateConfigLabels();
        }
        int q = QualitySettings.GetQualityLevel();
        UpdateConfigQualityButtons(Mathf.Clamp(q, 0, 3));

        _configModalOverlay.style.display = DisplayStyle.Flex;
        _configModalBox.style.display     = DisplayStyle.Flex;
    }

    void CloseConfigModal()
    {
        _configModalOverlay.style.display = DisplayStyle.None;
        _configModalBox.style.display     = DisplayStyle.None;
    }

    void UpdateConfigLabels()
    {
        var am = AudioManager.Instance;
        if (am == null) return;
        if (_configLabelMaster != null)
            _configLabelMaster.text = $"{Mathf.RoundToInt(am.MasterVolume * 100)}%";
        if (_configLabelMusic != null)
            _configLabelMusic.text  = $"{Mathf.RoundToInt(am.MusicVolume  * 100)}%";
        if (_configLabelSFX != null)
            _configLabelSFX.text    = $"{Mathf.RoundToInt(am.SFXVolume    * 100)}%";
    }

    void SelectConfigQuality(int index)
    {
        AudioManager.Instance?.SetQualityLevel(index);
        UpdateConfigQualityButtons(index);
    }

    void UpdateConfigQualityButtons(int activeIndex)
    {
        var buttons = new[] { _configBtnQualityLow, _configBtnQualityMedium, _configBtnQualityHigh, _configBtnQualityUltra };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].EnableInClassList("settings-quality-btn--active", i == activeIndex);
        }
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
        CursorManager.Instance?.SetDefaultCursor();
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
        CursorManager.Instance?.SetDefaultCursor();
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
        ScreenManager.Instance.GoTo(GameScreen.Editor);
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