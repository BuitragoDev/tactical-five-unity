using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class RecordsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private VisualElement _historicalBody;
    private VisualElement _teamRecordsBody;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<HistoricalRecordData> _historicalRecords;
    private List<TeamRecordData> _teamRecords;

    private Dictionary<string, Sprite> _logoSprites = new();

    private static readonly Dictionary<string, string> StatLabels = new()
    {
        { "points", "PUNTOS" },
        { "rebounds", "REBOTES" },
        { "assists", "ASISTENCIAS" },
        { "steals", "ROBOS" },
        { "blocks", "TAPONES" },
        { "fgm", "TIROS" },
        { "fg3m", "TRIPLES" },
        { "ftm", "TIROS LIBRES" },
        { "turnovers", "PÉRDIDAS" }
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
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _historicalBody = _root.Q<VisualElement>("HistoricalBody");
        _teamRecordsBody = _root.Q<VisualElement>("TeamRecordsBody");
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _historicalRecords = DatabaseManager.Instance.GetAllHistoricalRecords();
        _teamRecords = DatabaseManager.Instance.GetTeamRecords(_myTeam.id);
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ => ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("BtnReset")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.MainMenu));
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

    void Refresh()
    {
        RefreshHeader();
        BuildHistoricalRecords();
        BuildTeamRecords();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
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
            var nextGame = DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id);
            _root.Q<Label>("HeaderDate").text = nextGame != null
                ? System.DateTime.Parse(nextGame.game_date).ToString("dd/MM/yyyy") : "";
        }

        _btnAction.text = "← DASHBOARD";
    }

    void BuildHistoricalRecords()
    {
        _historicalBody.Clear();

        foreach (var record in _historicalRecords)
        {
            var row = new VisualElement();
            row.AddToClassList("record-row");

            var statLbl = new Label();
            statLbl.AddToClassList("record-stat");
            statLbl.text = StatLabels.TryGetValue(record.stat_type, out var label) ? label : record.stat_type;

            var valLbl = new Label();
            valLbl.AddToClassList("record-value");
            valLbl.text = record.value.ToString();

            var playerLbl = new Label();
            playerLbl.AddToClassList("record-player");
            playerLbl.text = record.player_name;

            var teamLbl = new Label();
            teamLbl.AddToClassList("record-team");
            teamLbl.text = record.team_abbreviation;

            var dateLbl = new Label();
            dateLbl.AddToClassList("record-date");
            try
            {
                var dt = System.DateTime.Parse(record.game_date);
                dateLbl.text = dt.ToString("dd/MM/yyyy");
            }
            catch
            {
                dateLbl.text = record.game_date;
            }

            row.Add(statLbl);
            row.Add(valLbl);
            row.Add(playerLbl);
            row.Add(teamLbl);
            row.Add(dateLbl);

            _historicalBody.Add(row);
        }
    }

    void BuildTeamRecords()
    {
        _teamRecordsBody.Clear();

        foreach (var record in _teamRecords)
        {
            var row = new VisualElement();
            row.AddToClassList("record-row");

            var statLbl = new Label();
            statLbl.AddToClassList("record-stat");
            statLbl.text = StatLabels.TryGetValue(record.stat_type, out var label) ? label : record.stat_type;

            var valLbl = new Label();
            valLbl.AddToClassList("record-value");
            valLbl.text = record.value.ToString();

            var playerLbl = new Label();
            playerLbl.AddToClassList("record-player");
            playerLbl.text = record.player_name;

            var teamLbl = new Label();
            teamLbl.AddToClassList("record-team");
            teamLbl.text = _myTeam.abbreviation;

            var dateLbl = new Label();
            dateLbl.AddToClassList("record-date");
            try
            {
                var dt = System.DateTime.Parse(record.game_date);
                dateLbl.text = dt.ToString("dd/MM/yyyy");
            }
            catch
            {
                dateLbl.text = record.game_date;
            }

            row.Add(statLbl);
            row.Add(valLbl);
            row.Add(playerLbl);
            row.Add(teamLbl);
            row.Add(dateLbl);

            _teamRecordsBody.Add(row);
        }
    }
}
