using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class SettingsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Slider _sliderMaster;
    private Slider _sliderMusic;
    private Slider _sliderSFX;
    private Label _labelMaster;
    private Label _labelMusic;
    private Label _labelSFX;
    private DropdownField _dropdownQuality;
    private Button _btnMainMenu;
    private Button _btnExit;
    private Button _btnAction;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;

    private readonly List<string> _qualityNames = new List<string>
    {
        "Baja", "Media", "Alta", "Ultra"
    };

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
        _sliderMaster = _root.Q<Slider>("SliderMaster");
        _sliderMusic  = _root.Q<Slider>("SliderMusic");
        _sliderSFX    = _root.Q<Slider>("SliderSFX");
        _labelMaster  = _root.Q<Label>("LabelMaster");
        _labelMusic   = _root.Q<Label>("LabelMusic");
        _labelSFX     = _root.Q<Label>("LabelSFX");
        _dropdownQuality = _root.Q<DropdownField>("DropdownQuality");
        _btnMainMenu  = _root.Q<Button>("BtnMainMenu");
        _btnExit      = _root.Q<Button>("BtnExit");
        _btnAction    = _root.Q<Button>("BtnAction");
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

        _sliderMaster?.RegisterValueChangedCallback(evt =>
        {
            AudioManager.Instance?.SetMasterVolume(evt.newValue);
            UpdateLabels();
        });

        _sliderMusic?.RegisterValueChangedCallback(evt =>
        {
            AudioManager.Instance?.SetMusicVolume(evt.newValue);
            UpdateLabels();
        });

        _sliderSFX?.RegisterValueChangedCallback(evt =>
        {
            AudioManager.Instance?.SetSFXVolume(evt.newValue);
            UpdateLabels();
        });

        _dropdownQuality?.RegisterValueChangedCallback(evt =>
        {
            int idx = _dropdownQuality.index;
            AudioManager.Instance?.SetQualityLevel(idx);
        });

        _btnMainMenu?.RegisterCallback<ClickEvent>(_ =>
        {
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });

        _btnExit?.RegisterCallback<ClickEvent>(_ =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Dashboard));
    }

    void RegisterNavButtons()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Roster));
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Calendar));
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Standings));
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Palmares));
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Results));
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Playoffs));
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Stats));
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Records));
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Market));
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Finances));
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Sponsors));
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.TV));
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Arena));
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Messages));
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

        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
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
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetNextGameDateString(_manager.id, _myTeam.id);
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

        _dropdownQuality.choices = _qualityNames;
        int currentQuality = QualitySettings.GetQualityLevel();
        _dropdownQuality.index = Mathf.Clamp(currentQuality, 0, _qualityNames.Count - 1);
    }

    void UpdateLabels()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        _labelMaster.text = $"{Mathf.RoundToInt(am.MasterVolume * 100)}%";
        _labelMusic.text  = $"{Mathf.RoundToInt(am.MusicVolume  * 100)}%";
        _labelSFX.text   = $"{Mathf.RoundToInt(am.SFXVolume    * 100)}%";
    }
}
