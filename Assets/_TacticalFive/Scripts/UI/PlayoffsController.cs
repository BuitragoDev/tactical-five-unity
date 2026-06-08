using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class PlayoffsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private VisualElement _regularSeasonEmpty;
    private VisualElement _playInPanel;
    private VisualElement _playInBody;
    private Label _noPlayInText;
    private VisualElement _eastBracket;
    private VisualElement _westBracket;
    private VisualElement _finalsBody;
    private Label _noFinalsText;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;
    private List<GameData> _playoffGames;
    private List<GameData> _playInGames;

    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();
    private Dictionary<string, Sprite> _logoSprites32 = new();

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
        _btnAction = _root.Q<Button>("BtnAction");
        _regularSeasonEmpty = _root.Q<VisualElement>("RegularSeasonEmpty");
        _playInPanel = _root.Q<VisualElement>("PlayInPanel");
        _playInBody = _root.Q<VisualElement>("PlayInBody");
        _noPlayInText = _root.Q<Label>("NoPlayInText");
        _eastBracket = _root.Q<VisualElement>("EastBracket");
        _westBracket = _root.Q<VisualElement>("WestBracket");
        _finalsBody = _root.Q<VisualElement>("FinalsBody");
        _noFinalsText = _root.Q<Label>("NoFinalsText");
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        var logos32 = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        if (_myTeam == null) return;
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _playoffGames = DatabaseManager.Instance.GetPlayoffGames(_manager.id);
        if (_playoffGames == null) _playoffGames = new List<GameData>();
        _playInGames = DatabaseManager.Instance.GetPlayInGames(_manager.id);
        if (_playInGames == null) _playInGames = new List<GameData>();
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
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
        _root.Q<Button>("NavConfig")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });
    }

    void Refresh()
    {
        RefreshHeader();

        string phase = _season?.phase ?? "regular";

        if (phase == "regular" || phase == "preseason")
        {
            _regularSeasonEmpty.style.display = DisplayStyle.Flex;
            _playInPanel.style.display = DisplayStyle.None;
            if (_eastBracket != null && _eastBracket.parent != null) _eastBracket.parent.style.display = DisplayStyle.None;
            if (_westBracket != null && _westBracket.parent != null) _westBracket.parent.style.display = DisplayStyle.None;
            if (_finalsBody != null && _finalsBody.parent != null) _finalsBody.parent.style.display = DisplayStyle.None;
            return;
        }

        _regularSeasonEmpty.style.display = DisplayStyle.None;
        _playInPanel.style.display = DisplayStyle.Flex;
        if (_eastBracket != null && _eastBracket.parent != null) _eastBracket.parent.style.display = DisplayStyle.Flex;
        if (_westBracket != null && _westBracket.parent != null) _westBracket.parent.style.display = DisplayStyle.Flex;
        if (_finalsBody != null && _finalsBody.parent != null) _finalsBody.parent.style.display = DisplayStyle.Flex;

        BuildPlayIn();
        BuildEastBracket();
        BuildWestBracket();
        BuildFinals();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites64.TryGetValue(_myTeam.logo, out var sprite))
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

    void BuildPlayIn()
    {
        _playInBody.Clear();

        if (_playInGames.Count == 0)
        {
            _noPlayInText.style.display = DisplayStyle.Flex;
            return;
        }

        _noPlayInText.style.display = DisplayStyle.None;

        var grouped = _playInGames.GroupBy(g => g.series_label);
        foreach (var series in grouped)
        {
            var seriesElem = CreateSeriesBlock(series.Key, series.ToList(), "Play-In");
            _playInBody.Add(seriesElem);
        }
    }

    void BuildEastBracket()
    {
        _eastBracket.Clear();
        var eastGames = _playoffGames.Where(g =>
        {
            var home = _allTeams.Find(t => t.id == g.home_team_id);
            return home != null && home.conference == "East";
        }).ToList();

        BuildConferenceBracket(_eastBracket, eastGames, "East");
    }

    void BuildWestBracket()
    {
        _westBracket.Clear();
        var westGames = _playoffGames.Where(g =>
        {
            var home = _allTeams.Find(t => t.id == g.home_team_id);
            return home != null && home.conference == "West";
        }).ToList();

        BuildConferenceBracket(_westBracket, westGames, "West");
    }

    void BuildConferenceBracket(VisualElement container, List<GameData> games, string conf)
    {
        string confLower = conf.ToLower();
        var roundNames = new Dictionary<string, string>
        {
            { "r1", "Primera Ronda" },
            { "r2", "Semifinales de Conferencia" },
            { "r3", "Finales de Conferencia" }
        };

        foreach (var round in roundNames)
        {
            var roundHeader = new Label();
            roundHeader.AddToClassList("playoff-round-name");
            roundHeader.text = round.Value;
            container.Add(roundHeader);

            var roundGames = games.Where(g => g.series_label.Contains($"playoff-{round.Key}-{confLower}")).ToList();
            var grouped = roundGames.GroupBy(g => g.series_label);

            if (roundGames.Count == 0)
            {
                var placeholder = new VisualElement();
                placeholder.AddToClassList("playoff-placeholder");
                var placeholderText = new Label();
                placeholderText.AddToClassList("playoff-placeholder-text");
                placeholderText.text = round.Key == "r1"
                    ? "Esperando resultados de la primera ronda..."
                    : round.Key == "r2"
                        ? "Esperando resultados de semifinales..."
                        : "Esperando resultados de finales de conferencia...";
                placeholder.Add(placeholderText);
                container.Add(placeholder);
            }
            else
            {
                foreach (var series in grouped)
                {
                    var seriesElem = CreateSeriesBlock(series.Key, series.ToList(), "Playoffs");
                    container.Add(seriesElem);
                }
            }
        }
    }

    void BuildFinals()
    {
        _finalsBody.Clear();

        var finalsGames = _playoffGames.Where(g => g.series_label == "playoff-r4-finals").ToList();
        if (finalsGames.Count == 0)
        {
            _noFinalsText.style.display = DisplayStyle.Flex;
            return;
        }

        _noFinalsText.style.display = DisplayStyle.None;

        var seriesElem = CreateSeriesBlock("Finals NBA", finalsGames, "Finals");
        _finalsBody.Add(seriesElem);
    }

    VisualElement CreateSeriesBlock(string seriesLabel, List<GameData> games, string type)
    {
        var block = new VisualElement();
        block.AddToClassList("playoff-series");

        bool isMyTeam = games.Any(g => g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id);
        if (isMyTeam) block.AddToClassList("playoff-series--my-team");

        var header = new VisualElement();
        header.AddToClassList("playoff-series-header");

        var label = new Label();
        label.AddToClassList("playoff-round-label");
        label.text = FormatSeriesLabel(seriesLabel);

        var typeLbl = new Label();
        typeLbl.AddToClassList("playoff-series-type");
        typeLbl.text = type == "Playoffs" || type == "Finals" ? "Best of 7" : "Single Game";

        header.Add(label);
        header.Add(typeLbl);
        block.Add(header);

        foreach (var g in games.OrderBy(g => g.game_day))
        {
            var row = CreatePlayoffGameRow(g);
            block.Add(row);
        }

        // Series matchup summary
        var matchup = ComputeSeriesMatchup(games);
        if (matchup.HasValue)
        {
            var m = matchup.Value;
            var matchupRow = new VisualElement();
            matchupRow.AddToClassList("playoff-series-matchup");

            var topTeam = new Label();
            topTeam.AddToClassList("playoff-matchup-team");
            topTeam.text = m.topAbbr;
            if (m.topWins > m.bottomWins) topTeam.AddToClassList("playoff-matchup-leader");

            var score = new Label();
            score.AddToClassList("playoff-matchup-score");
            score.text = $"{m.topWins} - {m.bottomWins}";

            var bottomTeam = new Label();
            bottomTeam.AddToClassList("playoff-matchup-team");
            bottomTeam.text = m.bottomAbbr;
            if (m.bottomWins > m.topWins) bottomTeam.AddToClassList("playoff-matchup-leader");

            matchupRow.Add(topTeam);
            matchupRow.Add(score);
            matchupRow.Add(bottomTeam);
            block.Add(matchupRow);
        }

        return block;
    }

    VisualElement CreatePlayoffGameRow(GameData game)
    {
        var row = new VisualElement();
        row.AddToClassList("playoff-game-row");
        bool isMyGame = game.home_team_id == _myTeam.id || game.away_team_id == _myTeam.id;
        if (isMyGame) row.AddToClassList("playoff-game-row--my-game");

        var home = _allTeams.Find(t => t.id == game.home_team_id);
        var away = _allTeams.Find(t => t.id == game.away_team_id);

        var teamsBlock = new VisualElement();
        teamsBlock.style.flexDirection = FlexDirection.Row;
        teamsBlock.AddToClassList("playoff-game-teams");

        // Home: name + logo32
        var homeBlock = new VisualElement();
        homeBlock.style.flexDirection = FlexDirection.Row;
        homeBlock.AddToClassList("playoff-team-block playoff-team-block--home");

        var homeName = new Label();
        homeName.AddToClassList("playoff-team-name");
        homeName.text = home?.name ?? "???";
        homeBlock.Add(homeName);

        var homeLogo = new VisualElement();
        homeLogo.AddToClassList("playoff-team-logo");
        if (home != null && _logoSprites32.TryGetValue(home.logo, out var hSprite))
            homeLogo.style.backgroundImage = new StyleBackground(hSprite);
        homeBlock.Add(homeLogo);

        teamsBlock.Add(homeBlock);

        // Score: awayScore - homeScore
        var scoreBlock = new VisualElement();
        scoreBlock.AddToClassList("playoff-score-block");

        var awayScore = new Label();
        awayScore.AddToClassList("playoff-score");
        if (game.is_played == 1 && game.away_score > game.home_score)
            awayScore.AddToClassList("playoff-score--winner");
        awayScore.text = game.is_played == 1 ? game.away_score.ToString() : "-";
        scoreBlock.Add(awayScore);

        var sep = new Label();
        sep.AddToClassList("playoff-score-sep");
        sep.text = "-";
        scoreBlock.Add(sep);

        var homeScore = new Label();
        homeScore.AddToClassList("playoff-score");
        if (game.is_played == 1 && game.home_score > game.away_score)
            homeScore.AddToClassList("playoff-score--winner");
        homeScore.text = game.is_played == 1 ? game.home_score.ToString() : "-";
        scoreBlock.Add(homeScore);

        teamsBlock.Add(scoreBlock);

        // Away: logo32 + name
        var awayBlock = new VisualElement();
        awayBlock.style.flexDirection = FlexDirection.Row;
        awayBlock.AddToClassList("playoff-team-block playoff-team-block--away");

        var awayLogo = new VisualElement();
        awayLogo.AddToClassList("playoff-team-logo");
        if (away != null && _logoSprites32.TryGetValue(away.logo, out var aSprite))
            awayLogo.style.backgroundImage = new StyleBackground(aSprite);
        awayBlock.Add(awayLogo);

        var awayName = new Label();
        awayName.AddToClassList("playoff-team-name");
        awayName.text = away?.name ?? "???";
        awayBlock.Add(awayName);

        teamsBlock.Add(awayBlock);

        row.Add(teamsBlock);

        var typeLbl = new Label();
        typeLbl.AddToClassList("playoff-game-type");
        typeLbl.text = $"G{game.game_day}";
        row.Add(typeLbl);

        return row;
    }

    void LoadSidebarIcons()
    {
        var iconMap = new System.Collections.Generic.Dictionary<string, string>
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

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }

    string FormatSeriesLabel(string label)
    {
        if (label.StartsWith("playin-7-8-")) return "7 vs 8";
        if (label.StartsWith("playin-9-10-")) return "9 vs 10";
        if (label.StartsWith("playin-elim-")) return "Eliminatoria";
        if (label == "playoff-r4-finals") return "Final NBA";

        // Remove "playoff-rX-" prefix
        var parts = label.Split('-');
        if (parts.Length >= 3 && parts[0] == "playoff")
        {
            return string.Join("-", parts.Skip(2));
        }
        return label;
    }

    (string topAbbr, string bottomAbbr, int topWins, int bottomWins)? ComputeSeriesMatchup(List<GameData> games)
    {
        if (games.Count == 0) return null;
        var first = games[0];
        var home = _allTeams.Find(t => t.id == first.home_team_id);
        var away = _allTeams.Find(t => t.id == first.away_team_id);
        if (home == null || away == null) return null;

        int homeWins = 0, awayWins = 0;
        foreach (var g in games)
        {
            if (g.is_played != 1) continue;
            if (g.home_score > g.away_score)
            {
                if (g.home_team_id == home.id) homeWins++;
                else awayWins++;
            }
            else
            {
                if (g.away_team_id == home.id) homeWins++;
                else awayWins++;
            }
        }
        return (home.abbreviation, away.abbreviation, homeWins, awayWins);
    }
}
