using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class RecordsController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Records;
    private Button _tabTeam;
    private Button _tabSeason;
    private Button _tabHistorical;
    private VisualElement _recordsBody;
    private VisualElement _headerTeamCol;
    private List<TeamData> _allTeams;
    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();
    private string _currentFilter = "team";
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
    private static readonly string[] StatOrder = {
        "points", "rebounds", "assists", "steals", "blocks",
        "fgm", "fg3m", "ftm", "turnovers"
    };
    protected override void CacheReferences()
    {
        _tabTeam = _root.Q<Button>("TabTeam");
        _tabSeason = _root.Q<Button>("TabSeason");
        _tabHistorical = _root.Q<Button>("TabHistorical");
        _recordsBody = _root.Q<VisualElement>("RecordsBody");
        _headerTeamCol = _root.Q<VisualElement>("HeaderTeamCol");
    }
    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        
        

        
        
        _allTeams = DatabaseManager.Instance.GetAllTeams();
    }
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _tabTeam?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetFilter("team"); });
        _tabSeason?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetFilter("season"); });
        _tabHistorical?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetFilter("historical"); });
    }

    void SetFilter(string filter)
    {
        _currentFilter = filter;
        _tabTeam.RemoveFromClassList("records-tab--active");
        _tabSeason.RemoveFromClassList("records-tab--active");
        _tabHistorical.RemoveFromClassList("records-tab--active");

        if (filter == "team") _tabTeam.AddToClassList("records-tab--active");
        else if (filter == "season") _tabSeason.AddToClassList("records-tab--active");
        else if (filter == "historical") _tabHistorical.AddToClassList("records-tab--active");

        Refresh();
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Records] RefreshHeader error: {ex.Message}"); }
        BuildRecords();
    }
    protected override void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites64.TryGetValue(_myTeam.logo, out var sprite))
            _root.Q<VisualElement>("HeaderTeamLogo").style.backgroundImage = new StyleBackground(sprite);

        _root.Q<Label>("HeaderTeamName").text = _myTeam.name.ToUpper();
        _root.Q<Label>("HeaderManagerName").text = $"Manager: {_manager.name}";
        var budgetLabel = _root.Q<Label>("HeaderBudget");
        budgetLabel.text = $"${_myTeam.budget / 1_000_000}M";
        budgetLabel.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long totalPayroll = players.Sum(p => p.salary);
        _root.Q<Label>("HeaderPayroll").text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - totalPayroll;
        var marginLbl = _root.Q<Label>("HeaderMargin");
        string marginText = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        marginLbl.text = marginText;
        var chemLabel = _root.Q<Label>("HeaderChemistry");
        if (chemLabel != null)
        {
            chemLabel.text = $"{chemistry.ToString()}%";
            chemLabel.RemoveFromClassList("header-stat-value--gold");
            chemLabel.RemoveFromClassList("header-stat-value--negative");
            if (chemistry < 40)
                chemLabel.AddToClassList("header-stat-value--negative");
            else if (chemistry < 70)
                chemLabel.AddToClassList("header-stat-value--gold");
        }
        marginLbl.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) marginLbl.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _root.Q<Label>("HeaderSeason").text = $"Temporada {_season.year_start}-{_season.year_end}";
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "MENÚ PRINCIPAL";
    }

    void BuildRecords()
    {
        _recordsBody.Clear();

        _headerTeamCol.style.display = DisplayStyle.Flex;

        if (_currentFilter == "team")
            BuildTeamRecords();
        else if (_currentFilter == "season")
            BuildSeasonRecords();
        else
            BuildHistoricalRecords();
    }

    void BuildTeamRecords()
    {
        if (_myTeam == null) return;
        var records = DatabaseManager.Instance.GetTeamRecords(_myTeam.id);
        var byStat = records.ToDictionary(r => r.stat_type, r => r);

        int count = 0;
        foreach (var stat in StatOrder)
        {
            if (!byStat.TryGetValue(stat, out var rec)) continue;
            var row = CreateRow(stat, rec.player_name, rec.value.ToString(), rec.game_date, null);
            _recordsBody.Add(row);
            count++;
        }

        if (count == 0) ShowEmpty("No hay récords del equipo todavía.");
    }

    void BuildSeasonRecords()
    {
        if (_season == null) return;
        var records = DatabaseManager.Instance.GetCurrentSeasonRecords(_season.id);
        var byStat = records.ToDictionary(r => r.stat_type, r => r);

        int count = 0;
        foreach (var stat in StatOrder)
        {
            if (!byStat.TryGetValue(stat, out var rec)) continue;
            var team = _allTeams?.Find(t => t.id == rec.team_id);
            var row = CreateRow(stat, rec.player_name, rec.value.ToString(), rec.game_date, team);
            _recordsBody.Add(row);
            count++;
        }

        if (count == 0) ShowEmpty("No hay récords de temporada todavía. Juega partidos para ver récords.");
    }

    void BuildHistoricalRecords()
    {
        var records = DatabaseManager.Instance.GetAllHistoricalRecords();
        var byStat = records.ToDictionary(r => r.stat_type, r => r);

        foreach (var stat in StatOrder)
        {
            if (!byStat.TryGetValue(stat, out var rec)) continue;
            var team = _allTeams?.Find(t => t.abbreviation == rec.team_abbreviation);
            var row = CreateRow(stat, rec.player_name, rec.value.ToString(), rec.game_date, team);
            _recordsBody.Add(row);
        }
    }

    VisualElement CreateRow(string statType, string playerName, string value, string gameDate, TeamData team)
    {
        var row = new VisualElement();
        row.AddToClassList("record-row");

        var statLbl = new Label();
        statLbl.AddToClassList("record-stat");
        statLbl.text = StatLabels.TryGetValue(statType, out var label) ? label : statType;
        row.Add(statLbl);

        var valLbl = new Label();
        valLbl.AddToClassList("record-value");
        valLbl.text = value;
        row.Add(valLbl);

        var playerLbl = new Label();
        playerLbl.AddToClassList("record-player");
        playerLbl.text = playerName;
        row.Add(playerLbl);

        {
            var teamLbl = new Label();
            teamLbl.AddToClassList("record-team");
            teamLbl.text = team?.name ?? _myTeam?.name ?? "";
            row.Add(teamLbl);
        }

        var dateLbl = new Label();
        dateLbl.AddToClassList("record-date");
        try
        {
            var dt = System.DateTime.Parse(gameDate);
            dateLbl.text = dt.ToString("dd/MM/yyyy");
        }
        catch
        {
            dateLbl.text = gameDate;
        }
        row.Add(dateLbl);

        return row;
    }

    void ShowEmpty(string message)
    {
        var empty = new VisualElement();
        empty.AddToClassList("records-empty");
        var lbl = new Label();
        lbl.AddToClassList("records-empty-label");
        lbl.text = message;
        empty.Add(lbl);
        _recordsBody.Add(empty);
    }
}
