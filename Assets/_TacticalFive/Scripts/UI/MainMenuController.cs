using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuController : UIScreenController
{
    private Button _btnManager;
    private Button _btnProManager;
    private Button _btnLoadGame;
    private Button _btnEditor;
    private Button _btnExit;
    private Button _btnLegal;
    private Button _btnBugReport;

    private VisualElement _modalOverlay;
    private VisualElement _modalBox;
    private Button _btnCerrar;

    private VisualElement _bugReportOverlay;
    private VisualElement _bugReportBox;
    private VisualElement _bugReportInputContainer;
    private string _bugReportText = "";
    private Button _btnBugReportSend;
    private Button _btnBugReportCerrar;

    private VisualElement _exitModalOverlay;
    private VisualElement _exitModalBox;
    private Button _btnExitYes;
    private Button _btnExitNo;

    protected override void OnEnable()
    {
        base.OnEnable();

        CursorManager.Instance?.SetDefaultCursor();
        AudioManager.Instance?.PlayMusic("backgroundMenu");
    }

    protected override void CacheReferences()
    {
        _btnManager    = _root.Q<Button>("BtnManager");
        _btnProManager = _root.Q<Button>("BtnProManager");
        _btnLoadGame   = _root.Q<Button>("BtnLoadGame");
        _btnEditor     = _root.Q<Button>("BtnEditor");
        _btnExit       = _root.Q<Button>("BtnExit");
        _btnLegal      = _root.Q<Button>("BtnLegal");
        _btnBugReport  = _root.Q<Button>("BtnBugReport");

        // Modal Legal
        _modalOverlay = _root.Q<VisualElement>("ModalOverlay");
        _modalBox     = _root.Q<VisualElement>("ModalBox");
        _btnCerrar    = _root.Q<Button>("BtnCerrar");

        // Modal Bug Report
        _bugReportOverlay = _root.Q<VisualElement>("BugReportOverlay");
        _bugReportBox     = _root.Q<VisualElement>("BugReportBox");
        _bugReportInputContainer = _root.Q<VisualElement>("BugReportInputContainer");
        _btnBugReportSend = _root.Q<Button>("BtnBugReportSend");
        _btnBugReportCerrar = _root.Q<Button>("BtnBugReportCerrar");

        // Modal Salir
        _exitModalOverlay = _root.Q<VisualElement>("ExitModalOverlay");
        _exitModalBox     = _root.Q<VisualElement>("ExitModalBox");
        _btnExitYes       = _root.Q<Button>("BtnExitYes");
        _btnExitNo        = _root.Q<Button>("BtnExitNo");
    }

    protected override void RegisterCallbacks()
    {
        // Callbacks botones principales
        _btnManager?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnManagerClicked(); });
        _btnProManager?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnProManagerClicked(); });
        _btnLoadGame?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnLoadGameClicked(); });
        _btnEditor?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnEditorClicked(); });
        _btnExit?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnExitClicked(); });
        _btnLegal?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnLegalClicked(); });
        _btnBugReport?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnBugReportClicked(); });

        // Callbacks modal legal
        _btnCerrar?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseModal(); });
        _modalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _modalOverlay)
                { PlayClick(); CloseModal(); }
        });

        // Callbacks modal bug report
        _btnBugReportSend?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnBugReportSendClicked(); });
        _btnBugReportCerrar?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseBugReportModal(); });
        _bugReportOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _bugReportOverlay)
                { PlayClick(); CloseBugReportModal(); }
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

        // Cursores — diferidos por si CursorManager aún no está listo en primera carga
        _root.schedule.Execute(() =>
        {
            CursorManager.Instance?.SetDefaultCursor();
            if (CursorManager.Instance != null)
            {
                CursorManager.Instance.RegisterHandCursor(_btnManager);
                CursorManager.Instance.RegisterHandCursor(_btnProManager);
                CursorManager.Instance.RegisterHandCursor(_btnLoadGame);
                CursorManager.Instance.RegisterHandCursor(_btnEditor);
                CursorManager.Instance.RegisterHandCursor(_btnExit);
                CursorManager.Instance.RegisterHandCursor(_btnLegal);
                CursorManager.Instance.RegisterHandCursor(_btnBugReport);
                CursorManager.Instance.RegisterHandCursor(_btnCerrar);
                CursorManager.Instance.RegisterHandCursor(_btnBugReportSend);
                CursorManager.Instance.RegisterHandCursor(_btnBugReportCerrar);
                CursorManager.Instance.RegisterHandCursor(_btnExitYes);
                CursorManager.Instance.RegisterHandCursor(_btnExitNo);
                CursorManager.Instance.RegisterHandCursor(_root.Q<VisualElement>("ConfigIcon"));
                CursorManager.Instance.RegisterHandCursor(_btnConfigCerrar);
                CursorManager.Instance.RegisterHandCursor(_configBtnQualityLow);
                CursorManager.Instance.RegisterHandCursor(_configBtnQualityMedium);
                CursorManager.Instance.RegisterHandCursor(_configBtnQualityHigh);
                CursorManager.Instance.RegisterHandCursor(_configBtnQualityUltra);
            }
        }).StartingIn(100);

        // Escape para cerrar modales
        _root.focusable = true;
        _root.Focus();
        _root.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape)
            {
                if (_bugReportOverlay.style.display == DisplayStyle.Flex)
                    CloseBugReportModal();
                else if (_exitModalOverlay.style.display == DisplayStyle.Flex)
                    CloseExitModal();
                else if (_modalOverlay.style.display == DisplayStyle.Flex)
                    CloseModal();
            }
        });
    }

    protected override void InitConfigModal()
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
            if (_configSliderMaster?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderMaster.Container);
            if (_configSliderMusic?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderMusic.Container);
            if (_configSliderSFX?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderSFX.Container);
        }

        InitConfigSimToggle();
    }

    protected override void OpenConfigModal()
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
        UpdateConfigSimToggle();

        _configModalOverlay.style.display = DisplayStyle.Flex;
        _configModalBox.style.display     = DisplayStyle.Flex;
    }

    protected override void CloseConfigModal()
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

    void OnBugReportClicked()
    {
        OpenBugReportModal();
    }

    void OpenBugReportModal()
    {
        CursorManager.Instance?.SetDefaultCursor();
        _bugReportOverlay.style.display = DisplayStyle.Flex;
        _bugReportBox.style.display     = DisplayStyle.Flex;

        if (_bugReportInputContainer != null)
        {
            _bugReportInputContainer.Clear();
            var textField = new TextField();
            textField.multiline = true;
            textField.focusable = true;
            textField.style.flexGrow = 1;
            textField.style.width = Length.Percent(100);
            textField.style.height = 200;
            textField.style.backgroundColor = new Color(0.086f, 0.102f, 0.141f);
            textField.style.borderLeftWidth = 1;
            textField.style.borderRightWidth = 1;
            textField.style.borderTopWidth = 1;
            textField.style.borderBottomWidth = 1;
            textField.style.borderLeftColor = new Color(0.227f, 0.29f, 0.388f);
            textField.style.borderRightColor = new Color(0.227f, 0.29f, 0.388f);
            textField.style.borderTopColor = new Color(0.227f, 0.29f, 0.388f);
            textField.style.borderBottomColor = new Color(0.227f, 0.29f, 0.388f);
            textField.style.color = new Color(0.78f, 0.82f, 0.9f);
            textField.style.fontSize = 16;
            textField.style.paddingLeft = 12;
            textField.style.paddingRight = 12;
            textField.style.paddingTop = 12;
            textField.style.paddingBottom = 12;
            textField.style.whiteSpace = WhiteSpace.Normal;
            textField.style.unityTextAlign = TextAnchor.UpperLeft;

            var textInput = textField.Q(name: "unity-text-input");
            if (textInput != null)
            {
                textInput.style.backgroundColor = new Color(0.086f, 0.102f, 0.141f);
                textInput.style.color = new Color(0.78f, 0.82f, 0.9f);
                textInput.style.fontSize = 20;
                textInput.style.paddingLeft = 12;
                textInput.style.paddingRight = 12;
                textInput.style.paddingTop = 12;
                textInput.style.paddingBottom = 12;
                textInput.style.whiteSpace = WhiteSpace.Normal;
                textInput.style.unityTextAlign = TextAnchor.UpperLeft;
                textInput.style.flexGrow = 1;
            }

            textField.RegisterValueChangedCallback(evt =>
            {
                var v = evt.newValue;
                if (v.Length > 300)
                {
                    v = v.Substring(0, 300);
                    textField.SetValueWithoutNotify(v);
                }
                _bugReportText = v;
            });

            _bugReportInputContainer.Add(textField);
            _root.schedule.Execute(() => textField.Focus()).StartingIn(100);
        }
    }

    void CloseBugReportModal()
    {
        _bugReportOverlay.style.display = DisplayStyle.None;
        _bugReportBox.style.display     = DisplayStyle.None;
        _bugReportInputContainer?.Clear();
        _bugReportText = "";
    }

    void OnBugReportSendClicked()
    {
        string body = _bugReportText?.Trim();
        if (string.IsNullOrEmpty(body)) return;

        string subject = System.Uri.EscapeDataString("Bug Report - Tactical Five");
        string encodedBody = System.Uri.EscapeDataString(body);
        Application.OpenURL($"mailto:buitr4gosw@gmail.com?subject={subject}&body={encodedBody}");
        CloseBugReportModal();
    }
}