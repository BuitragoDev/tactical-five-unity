using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class SettingsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private CustomSlider _sliderMaster;
    private CustomSlider _sliderMusic;
    private CustomSlider _sliderSFX;
    private Label _labelMaster;
    private Label _labelMusic;
    private Label _labelSFX;
    private Button _btnQualityLow;
    private Button _btnQualityMedium;
    private Button _btnQualityHigh;
    private Button _btnQualityUltra;
    private Button _btnMainMenu;
    private Button _btnExit;
    private Button _btnAction;

    private VisualElement _mainMenuModalOverlay;
    private VisualElement _mainMenuModalBox;
    private Button _btnMainMenuYes;
    private Button _btnMainMenuNo;

    private VisualElement _exitModalOverlay;
    private VisualElement _exitModalBox;
    private Button _btnExitYes;
    private Button _btnExitNo;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        CacheReferences();
        LoadSidebarIcons();
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _sliderMaster = new CustomSlider(_root.Q<VisualElement>("SliderMaster"),
                                         _root.Q<VisualElement>("FillMaster"),
                                         _root.Q<VisualElement>("DraggerMaster"));
        _sliderMusic  = new CustomSlider(_root.Q<VisualElement>("SliderMusic"),
                                         _root.Q<VisualElement>("FillMusic"),
                                         _root.Q<VisualElement>("DraggerMusic"));
        _sliderSFX    = new CustomSlider(_root.Q<VisualElement>("SliderSFX"),
                                         _root.Q<VisualElement>("FillSFX"),
                                         _root.Q<VisualElement>("DraggerSFX"));
        _labelMaster  = _root.Q<Label>("LabelMaster");
        _labelMusic   = _root.Q<Label>("LabelMusic");
        _labelSFX     = _root.Q<Label>("LabelSFX");
        _btnQualityLow    = _root.Q<Button>("BtnQualityLow");
        _btnQualityMedium = _root.Q<Button>("BtnQualityMedium");
        _btnQualityHigh   = _root.Q<Button>("BtnQualityHigh");
        _btnQualityUltra  = _root.Q<Button>("BtnQualityUltra");
        _btnMainMenu  = _root.Q<Button>("BtnMainMenu");
        _btnExit      = _root.Q<Button>("BtnExit");
        _btnAction    = _root.Q<Button>("BtnAction");

        _mainMenuModalOverlay = _root.Q<VisualElement>("MainMenuModalOverlay");
        _mainMenuModalBox     = _root.Q<VisualElement>("MainMenuModalBox");
        _btnMainMenuYes       = _root.Q<Button>("BtnMainMenuYes");
        _btnMainMenuNo        = _root.Q<Button>("BtnMainMenuNo");

        _exitModalOverlay = _root.Q<VisualElement>("ExitModalOverlay");
        _exitModalBox     = _root.Q<VisualElement>("ExitModalBox");
        _btnExitYes       = _root.Q<Button>("BtnExitYes");
        _btnExitNo        = _root.Q<Button>("BtnExitNo");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance?.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();

        _sliderMaster.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMasterVolume(v);
            UpdateLabels();
        };

        _sliderMusic.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMusicVolume(v);
            UpdateLabels();
        };

        _sliderSFX.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetSFXVolume(v);
            UpdateLabels();
        };

        _btnQualityLow?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectQuality(0); });
        _btnQualityMedium?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectQuality(1); });
        _btnQualityHigh?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectQuality(2); });
        _btnQualityUltra?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectQuality(3); });

        _btnMainMenu?.RegisterCallback<ClickEvent>(_ => OpenMainMenuModal());
        _btnExit?.RegisterCallback<ClickEvent>(_ => OpenExitModal());

        _btnMainMenuYes?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        _btnMainMenuNo?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseMainMenuModal();
        });
        _mainMenuModalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _mainMenuModalOverlay)
                { PlayClick(); CloseMainMenuModal(); }
        });

        _btnExitYes?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            QuitGame();
        });
        _btnExitNo?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseExitModal();
        });
        _exitModalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _exitModalOverlay)
                { PlayClick(); CloseExitModal(); }
        });

        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
    }

    void RegisterNavButtons()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Roster); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Palmares); });
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Records); });
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Market); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Finances); });
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
    }

    void LoadSidebarIcons()
    {
        var iconMap = new Dictionary<string, string>
        {
            {"NavDashboardIcon", "inicio"},
            {"NavRosterIcon", "plantilla"},
            {"NavCalendarIcon", "calendario"},
            {"NavStandingsIcon", "clasificacion"},
            {"NavPalmaresIcon", "palmares"},
            {"NavResultsIcon", "resultados"},
            {"NavPlayoffsIcon", "playoff"},
            {"NavStatsIcon", "estadisticas"},
            {"NavRecordsIcon", "records"},
            {"NavMarketIcon", "mercado"},
            {"NavFinancesIcon", "finanzas"},
            {"NavSponsorsIcon", "patrocinador"},
            {"NavTVIcon", "television"},
            {"NavArenaIcon", "pabellon"},
            {"NavMessagesIcon", "mensajes"},
            {"NavConfigIcon", "configuracion"}
        };

        foreach (var kv in iconMap)
        {
            var iconElem = _root.Q<VisualElement>(kv.Key);
            if (iconElem == null) continue;
            var tex = Resources.Load<Texture2D>($"Icons/{kv.Value}");
            if (tex != null)
                iconElem.style.backgroundImage = new StyleBackground(tex);
        }
    }

    void Refresh()
    {
        RefreshHeader();
        RefreshSettings();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos) logoDict[s.name] = s;

        if (logoDict.TryGetValue(_myTeam.logo, out var sprite))
            _root.Q<VisualElement>("HeaderTeamLogo").style.backgroundImage = new StyleBackground(sprite);

        _root.Q<Label>("HeaderTeamName").text = _myTeam.name.ToUpper();
        _root.Q<Label>("HeaderManagerName").text = $"Manager: {_manager.name}";
        _root.Q<Label>("HeaderBudget").text = $"${_myTeam.budget / 1_000_000}M";

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long totalPayroll = players.Sum(p => p.salary);
        _root.Q<Label>("HeaderPayroll").text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? 155_000_000;
        long margin = salaryCap - totalPayroll;
        var marginLbl = _root.Q<Label>("HeaderMargin");
        marginLbl.text = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        marginLbl.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) marginLbl.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _root.Q<Label>("HeaderSeason").text = $"Temporada {_season.year_start}-{_season.year_end}";
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "DASHBOARD";
    }

    void RefreshSettings()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        _sliderMaster.SetValueWithoutNotify(am.MasterVolume);
        _sliderMusic.SetValueWithoutNotify(am.MusicVolume);
        _sliderSFX.SetValueWithoutNotify(am.SFXVolume);
        UpdateLabels();

        int currentQuality = QualitySettings.GetQualityLevel();
        UpdateQualityButtons(Mathf.Clamp(currentQuality, 0, 3));
    }

    void UpdateLabels()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        _labelMaster.text = $"{Mathf.RoundToInt(am.MasterVolume * 100)}%";
        _labelMusic.text  = $"{Mathf.RoundToInt(am.MusicVolume  * 100)}%";
        _labelSFX.text    = $"{Mathf.RoundToInt(am.SFXVolume    * 100)}%";
    }

    void SelectQuality(int index)
    {
        AudioManager.Instance?.SetQualityLevel(index);
        UpdateQualityButtons(index);
    }

    void UpdateQualityButtons(int activeIndex)
    {
        var buttons = new[] { _btnQualityLow, _btnQualityMedium, _btnQualityHigh, _btnQualityUltra };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].EnableInClassList("settings-quality-btn--active", i == activeIndex);
        }
    }

    void OpenMainMenuModal()
    {
        PlayClick();
        _mainMenuModalOverlay?.AddToClassList("modal-overlay--visible");
        _mainMenuModalBox?.AddToClassList("modal-box--visible");
    }

    void CloseMainMenuModal()
    {
        _mainMenuModalOverlay?.RemoveFromClassList("modal-overlay--visible");
        _mainMenuModalBox?.RemoveFromClassList("modal-box--visible");
    }

    void OpenExitModal()
    {
        PlayClick();
        _exitModalOverlay?.AddToClassList("modal-overlay--visible");
        _exitModalBox?.AddToClassList("modal-box--visible");
    }

    void CloseExitModal()
    {
        _exitModalOverlay?.RemoveFromClassList("modal-overlay--visible");
        _exitModalBox?.RemoveFromClassList("modal-box--visible");
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
