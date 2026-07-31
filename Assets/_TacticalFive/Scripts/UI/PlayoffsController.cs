using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class PlayoffsController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Playoffs;
    private ScrollView _scrollView;
    private VisualElement _regularSeasonEmpty;
    private VisualElement _playInPanel;
    private VisualElement _playInBody;
    private Label _noPlayInText;
    private VisualElement _eastBracket;
    private VisualElement _westBracket;
    private VisualElement _finalsBody;
    private Label _noFinalsText;
    private List<TeamData> _allTeams;
    private List<GameData> _playoffGames;
    private List<GameData> _playInGames;
    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();
    private Dictionary<string, Sprite> _logoSprites32 = new();
    protected override void CacheReferences()
    {
        _scrollView = _root.Q<ScrollView>("PlayoffsScroll");
        _regularSeasonEmpty = _root.Q<VisualElement>("RegularSeasonEmpty");
        _playInPanel = _root.Q<VisualElement>("PlayInPanel");
        _playInBody = _root.Q<VisualElement>("PlayInBody");
        _noPlayInText = _root.Q<Label>("NoPlayInText");
        _eastBracket = _root.Q<VisualElement>("EastBracket");
        _westBracket = _root.Q<VisualElement>("WestBracket");
        _finalsBody = _root.Q<VisualElement>("FinalsBody");
        _noFinalsText = _root.Q<Label>("NoFinalsText");
    }
    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        var logos32 = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        
        

        
        if (_myTeam == null) return;
        
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _playoffGames = DatabaseManager.Instance.GetPlayoffGames(_manager.id);
        if (_playoffGames == null) _playoffGames = new List<GameData>();
        _playInGames = DatabaseManager.Instance.GetPlayInGames(_manager.id);
        if (_playInGames == null) _playInGames = new List<GameData>();
    }
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        RegisterPanelToggles();
    }

    void RegisterPanelToggles()
    {
        var panels = new List<(VisualElement header, VisualElement panel)>();
        foreach (var panel in _root.Query<VisualElement>(null, "panel-playoffs").Build())
        {
            var header = panel.Q<VisualElement>(null, "panel-header");
            if (header != null)
                panels.Add((header, panel));
        }

        // Helper to update all arrows based on current toggle state
        void UpdateAllArrows()
        {
            foreach (var (hdr, pnl) in panels)
            {
                bool open = true;
                foreach (var child in pnl.Children())
                {
                    if (child == hdr) continue;
                    if (child.style.display == DisplayStyle.None) { open = false; break; }
                }
                var arrow = hdr.Q<Label>(null, "panel-toggle-arrow");
                if (arrow != null) arrow.text = open ? "▲" : "▼";
            }
        }

        foreach (var (header, panel) in panels)
        {
            header.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                bool isOpen = true;
                foreach (var child in panel.Children())
                {
                    if (child == header) continue;
                    if (child.style.display == DisplayStyle.None)
                    {
                        isOpen = false;
                        break;
                    }
                }

                // Close all panels
                foreach (var (_, otherPanel) in panels)
                {
                    foreach (var child in otherPanel.Children())
                    {
                        if (child == otherPanel.Q<VisualElement>(null, "panel-header")) continue;
                        child.style.display = DisplayStyle.None;
                    }
                }

                // Toggle this panel
                if (!isOpen)
                {
                    foreach (var child in panel.Children())
                    {
                        if (child == header) continue;
                        child.style.display = DisplayStyle.Flex;
                    }
                    if (_scrollView != null) _scrollView.scrollOffset = Vector2.zero;
                }

                UpdateAllArrows();
            });
        }

        UpdateAllArrows();
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Playoffs] RefreshHeader error: {ex.Message}"); }

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

        // Start with all panels collapsed
        foreach (var panel in _root.Query<VisualElement>(null, "panel-playoffs").Build())
        {
            foreach (var child in panel.Children())
            {
                if (child.ClassListContains("panel-header")) continue;
                child.style.display = DisplayStyle.None;
            }
            var header = panel.Q<VisualElement>(null, "panel-header");
            if (header != null)
            {
                var arrow = header.Q<Label>(null, "panel-toggle-arrow");
                if (arrow != null) arrow.text = "▼";
            }
        }
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
            { "r1", "PRIMERA RONDA" },
            { "r2", "SEMIFINALES DE CONFERENCIA" },
            { "r3", "FINALES DE CONFERENCIA" }
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
        typeLbl.text = type == "Playoffs" || type == "Finals" ? "Mejor de 7" : "Partido único";

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
        homeBlock.style.width = 260;
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

        // Score: homeScore - awayScore (home team on left)
        var scoreBlock = new VisualElement();
        scoreBlock.style.width = 80;
        scoreBlock.AddToClassList("playoff-score-block");

        var homeScore = new Label();
        homeScore.AddToClassList("playoff-score");
        if (game.is_played == 1 && game.home_score > game.away_score)
            homeScore.AddToClassList("playoff-score--winner");
        homeScore.text = game.is_played == 1 ? game.home_score.ToString() : "-";
        scoreBlock.Add(homeScore);

        var sep = new Label();
        sep.AddToClassList("playoff-score-sep");
        sep.text = "-";
        scoreBlock.Add(sep);

        var awayScore = new Label();
        awayScore.AddToClassList("playoff-score");
        if (game.is_played == 1 && game.away_score > game.home_score)
            awayScore.AddToClassList("playoff-score--winner");
        awayScore.text = game.is_played == 1 ? game.away_score.ToString() : "-";
        scoreBlock.Add(awayScore);

        teamsBlock.Add(scoreBlock);

        // Away: logo32 + name
        var awayBlock = new VisualElement();
        awayBlock.style.flexDirection = FlexDirection.Row;
        awayBlock.style.width = 260;
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

    string FormatSeriesLabel(string label)
    {
        // Helper: parse conference from last part
        string ParseConf(string lbl)
        {
            var parts = lbl.Split('-');
            if (parts.Length < 3) return "";
            string last = parts[parts.Length - 1].ToLower();
            if (last == "east") return "Este";
            if (last == "west") return "Oeste";
            return "";
        }

        // Play-in: playin-7-8-east, playin-9-10-west, playin-elim-east
        if (label.StartsWith("playin-"))
        {
            string conf = ParseConf(label);
            if (label.Contains("7-8")) return $"{conf} - 7º vs 8º";
            if (label.Contains("9-10")) return $"{conf} - 9º vs 10º";
            if (label.Contains("elim")) return $"{conf} - Eliminatoria";
            return label;
        }

        if (label == "playoff-r4-finals") return "Final NBA";

        // Playoff R1: playoff-r1-east-1v8 → "Este - 1º vs 8º"
        if (label.StartsWith("playoff-r1-"))
        {
            var parts = label.Split('-');
            if (parts.Length >= 4)
            {
                string conf = parts[2] == "east" ? "Este" : "Oeste";
                string matchup = parts[3];
                var seeds = matchup.Split('v');
                if (seeds.Length == 2)
                    return $"{conf} - {seeds[0]}º vs {seeds[1]}º";
                return $"{conf} - {matchup}";
            }
        }

        // Playoff R2: playoff-r2-east-s1 → "Este - Semifinal 1"
        if (label.StartsWith("playoff-r2-"))
        {
            var parts = label.Split('-');
            if (parts.Length >= 4)
            {
                string conf = parts[2] == "east" ? "Este" : "Oeste";
                string series = parts[3] == "s1" ? "Semifinal 1" : "Semifinal 2";
                return $"{conf} - {series}";
            }
        }

        // Playoff R3 (conference finals): playoff-r3-east-s1 → "Este - Final de Conferencia"
        if (label.StartsWith("playoff-r3-"))
        {
            string conf = label.Contains("east") ? "Este" : "Oeste";
            return $"{conf} - Final de Conferencia";
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