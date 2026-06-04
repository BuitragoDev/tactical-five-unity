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
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetNextGameDateString(_manager.id, _myTeam.id);
        }

        _btnAction.text = "← DASHBOARD";
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

        var grouped = eastGames.GroupBy(g => g.series_label);
        foreach (var series in grouped)
        {
            var seriesElem = CreateSeriesBlock(series.Key, series.ToList(), "Playoffs");
            _eastBracket.Add(seriesElem);
        }
    }

    void BuildWestBracket()
    {
        _westBracket.Clear();
        var westGames = _playoffGames.Where(g =>
        {
            var home = _allTeams.Find(t => t.id == g.home_team_id);
            return home != null && home.conference == "West";
        }).ToList();

        var grouped = westGames.GroupBy(g => g.series_label);
        foreach (var series in grouped)
        {
            var seriesElem = CreateSeriesBlock(series.Key, series.ToList(), "Playoffs");
            _westBracket.Add(seriesElem);
        }
    }

    void BuildFinals()
    {
        _finalsBody.Clear();

        var finalsGames = _playoffGames.Where(g => g.series_label == "Finals").ToList();
        if (finalsGames.Count == 0)
        {
            _noFinalsText.style.display = DisplayStyle.Flex;
            return;
        }

        _noFinalsText.style.display = DisplayStyle.None;

        var seriesElem = CreateSeriesBlock("Finals", finalsGames, "Finals");
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
        label.text = seriesLabel;

        var status = new Label();
        status.AddToClassList("playoff-series-status");
        int homeWins = games.Count(g => g.is_played == 1 && g.home_score > g.away_score);
        int awayWins = games.Count(g => g.is_played == 1 && g.away_score > g.home_score);
        status.text = $"{homeWins} - {awayWins}";

        header.Add(label);
        header.Add(status);
        block.Add(header);

        foreach (var g in games.OrderBy(g => g.game_day))
        {
            var row = CreatePlayoffGameRow(g);
            block.Add(row);
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
        teamsBlock.AddToClassList("playoff-game-teams");

        var awayBlock = new VisualElement();
        awayBlock.AddToClassList("playoff-team-block playoff-team-block--away");

        var awayName = new Label();
        awayName.AddToClassList("playoff-team-name");
        awayName.text = away?.abbreviation ?? "???";
        awayBlock.Add(awayName);

        var awayLogo = new VisualElement();
        awayLogo.AddToClassList("playoff-team-logo");
        if (away != null && _logoSprites.TryGetValue(away.logo, out var aSprite))
            awayLogo.style.backgroundImage = new StyleBackground(aSprite);
        awayBlock.Add(awayLogo);

        teamsBlock.Add(awayBlock);

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

        var homeBlock = new VisualElement();
        homeBlock.AddToClassList("playoff-team-block playoff-team-block--home");

        var homeLogo = new VisualElement();
        homeLogo.AddToClassList("playoff-team-logo");
        if (home != null && _logoSprites.TryGetValue(home.logo, out var hSprite))
            homeLogo.style.backgroundImage = new StyleBackground(hSprite);
        homeBlock.Add(homeLogo);

        var homeName = new Label();
        homeName.AddToClassList("playoff-team-name");
        homeName.text = home?.abbreviation ?? "???";
        homeBlock.Add(homeName);

        teamsBlock.Add(homeBlock);
        row.Add(teamsBlock);

        var typeLbl = new Label();
        typeLbl.AddToClassList("playoff-game-type");
        typeLbl.text = $"G{game.game_day}";
        row.Add(typeLbl);

        return row;
    }
}
