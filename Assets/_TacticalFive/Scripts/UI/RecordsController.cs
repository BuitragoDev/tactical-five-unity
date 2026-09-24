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
    private VisualElement _recordsHeaderIcon;
    private Label _recordsSubtitle;

    private List<TeamData> _allTeams;
    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();
    private Dictionary<string, Texture2D> _statIcons = new();
    private Texture2D _recordIcon;
    private Sprite _calendarSprite;

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

    private static readonly Dictionary<string, string> StatSubtitles = new()
    {
        { "points", "MÁXIMO DE PUNTOS EN UN PARTIDO" },
        { "rebounds", "MÁXIMO DE REBOTES EN UN PARTIDO" },
        { "assists", "MÁXIMO DE ASISTENCIAS EN UN PARTIDO" },
        { "steals", "MÁXIMO DE ROBOS EN UN PARTIDO" },
        { "blocks", "MÁXIMO DE TAPONES EN UN PARTIDO" },
        { "fgm", "MÁXIMO DE TIROS DE CAMPO EN UN PARTIDO" },
        { "fg3m", "MÁXIMO DE TRIPLES EN UN PARTIDO" },
        { "ftm", "MÁXIMO DE TIROS LIBRES EN UN PARTIDO" },
        { "turnovers", "MÁXIMO DE PÉRDIDAS EN UN PARTIDO" }
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
        _recordsHeaderIcon = _root.Q<VisualElement>("RecordsHeaderIcon");
        _recordsSubtitle = _root.Q<Label>("RecordsSubtitle");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var screenIcon = _root.Q<VisualElement>("RecordsScreenIcon");
        if (screenIcon != null)
        {
            var iconTex = Resources.Load<Texture2D>("Icons/records_icon");
            if (iconTex != null) screenIcon.style.backgroundImage = new StyleBackground(iconTex);
        }

        var logos = TeamImages.Logos(32);
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos64 = TeamImages.Logos(64);
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        _statIcons["points"] = Resources.Load<Texture2D>("Icons/puntos");
        _statIcons["rebounds"] = Resources.Load<Texture2D>("Icons/rebote");
        _statIcons["assists"] = Resources.Load<Texture2D>("Icons/pase");
        _statIcons["steals"] = Resources.Load<Texture2D>("Icons/robo");
        _statIcons["blocks"] = Resources.Load<Texture2D>("Icons/tapones");
        _statIcons["fgm"] = Resources.Load<Texture2D>("Icons/basket_icon");
        _statIcons["fg3m"] = Resources.Load<Texture2D>("Icons/basket_icon");
        _statIcons["ftm"] = Resources.Load<Texture2D>("Icons/basket_icon");
        _statIcons["turnovers"] = Resources.Load<Texture2D>("Icons/perdidas");

        _recordIcon = Resources.Load<Texture2D>("Icons/record_icon");
        _calendarSprite = Resources.Load<Sprite>("Icons/calendario");

        var headerIcon = Resources.Load<Texture2D>("Icons/records");
        if (headerIcon != null && _recordsHeaderIcon != null)
            _recordsHeaderIcon.style.backgroundImage = new StyleBackground(headerIcon);

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
            chemLabel.text = $"{chemistry}%";
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
        UpdateSubtitle();

        if (_currentFilter == "team")
            BuildTeamRecords();
        else if (_currentFilter == "season")
            BuildSeasonRecords();
        else
            BuildHistoricalRecords();
    }

    void UpdateSubtitle()
    {
        if (_recordsSubtitle == null) return;
        _recordsSubtitle.text = _currentFilter switch
        {
            "season" => "MEJORES REGISTROS DE LA TEMPORADA",
            "historical" => "MEJORES REGISTROS DE LA HISTORIA",
            _ => "MÁXIMOS HISTÓRICOS DEL EQUIPO"
        };
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
            _recordsBody.Add(CreateCard(stat, rec.player_name, rec.value.ToString(), rec.game_date, _myTeam, count == 0));
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
            var team = _allTeams?.Find(t => t.id == rec.team_id) ?? _myTeam;
            _recordsBody.Add(CreateCard(stat, rec.player_name, rec.value.ToString(), rec.game_date, team, count == 0));
            count++;
        }

        if (count == 0) ShowEmpty("No hay récords de temporada todavía. Juega partidos para ver récords.");
    }

    void BuildHistoricalRecords()
    {
        var records = DatabaseManager.Instance.GetAllHistoricalRecords();
        var byStat = records.ToDictionary(r => r.stat_type, r => r);

        int count = 0;
        foreach (var stat in StatOrder)
        {
            if (!byStat.TryGetValue(stat, out var rec)) continue;
            var team = _allTeams?.Find(t => t.abbreviation == rec.team_abbreviation);
            _recordsBody.Add(CreateCard(stat, rec.player_name, rec.value.ToString(), rec.game_date, team, count == 0));
            count++;
        }

        if (count == 0) ShowEmpty("No hay récords históricos todavía.");
    }

    VisualElement CreateCard(string statType, string playerName, string value, string gameDate, TeamData team, bool featured)
    {
        var card = new VisualElement();
        card.AddToClassList("record-card");
        if (featured) card.AddToClassList("record-card--featured");

        var inner = new VisualElement();
        inner.AddToClassList("record-card-inner");

        // Watermark
        if (_recordIcon != null)
        {
            var watermark = new VisualElement();
            watermark.AddToClassList("record-card-watermark");
            watermark.style.backgroundImage = new StyleBackground(_recordIcon);
            inner.Add(watermark);
        }

        // Head: badge + titles
        var head = new VisualElement();
        head.AddToClassList("record-card-head");

        var badge = new VisualElement();
        badge.AddToClassList("record-card-badge");
        _statIcons.TryGetValue(statType, out var statIcon);
        if (statIcon != null)
        {
            var badgeImg = new VisualElement();
            badgeImg.AddToClassList("record-card-badge-img");
            badgeImg.style.backgroundImage = new StyleBackground(statIcon);
            badge.Add(badgeImg);
        }
        head.Add(badge);

        var headText = new VisualElement();
        headText.AddToClassList("record-card-head-text");

        var title = new Label();
        title.AddToClassList("record-card-title");
        title.text = StatLabels.TryGetValue(statType, out var label) ? label : statType;
        headText.Add(title);

        var subtitle = new Label();
        subtitle.AddToClassList("record-card-subtitle");
        subtitle.text = StatSubtitles.TryGetValue(statType, out var sub) ? sub : "";
        headText.Add(subtitle);

        head.Add(headText);
        inner.Add(head);

        // Body: value + info
        var body = new VisualElement();
        body.AddToClassList("record-card-body");

        var valLbl = new Label();
        valLbl.AddToClassList("record-card-value");
        valLbl.text = value;
        body.Add(valLbl);

        var info = new VisualElement();
        info.AddToClassList("record-card-info");

        var playerLbl = new Label();
        playerLbl.AddToClassList("record-card-player");
        playerLbl.text = playerName;
        info.Add(playerLbl);

        // Team row
        var teamRow = new VisualElement();
        teamRow.AddToClassList("record-card-team-row");

        var teamLogo = new VisualElement();
        teamLogo.AddToClassList("record-card-team-logo");
        string logoName = team?.logo ?? _myTeam?.logo;
        if (!string.IsNullOrEmpty(logoName) && _logoSprites.TryGetValue(logoName, out var logoSprite))
            teamLogo.style.backgroundImage = new StyleBackground(logoSprite);
        teamRow.Add(teamLogo);

        var teamName = new Label();
        teamName.AddToClassList("record-card-team-name");
        teamName.text = team?.name ?? _myTeam?.name ?? "";
        teamRow.Add(teamName);
        info.Add(teamRow);

        // Date row
        var dateRow = new VisualElement();
        dateRow.AddToClassList("record-card-date-row");

        if (_calendarSprite != null)
        {
            var calIcon = new VisualElement();
            calIcon.AddToClassList("record-card-date-icon");
            calIcon.style.backgroundImage = new StyleBackground(_calendarSprite);
            dateRow.Add(calIcon);
        }

        var dateLbl = new Label();
        dateLbl.AddToClassList("record-card-date");
        try
        {
            var dt = System.DateTime.Parse(gameDate);
            dateLbl.text = dt.ToString("dd/MM/yyyy");
        }
        catch
        {
            dateLbl.text = gameDate;
        }
        dateRow.Add(dateLbl);
        info.Add(dateRow);

        body.Add(info);
        inner.Add(body);

        card.Add(inner);
        return card;
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
