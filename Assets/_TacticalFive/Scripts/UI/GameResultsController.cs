using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GameResultsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private VisualElement _gamesBody;
    private VisualElement _mvpPanel;
    private Label _mvpName, _mvpTeam, _mvpPos, _mvpPts, _mvpReb, _mvpAst, _mvpVal;
    private VisualElement _mvpLogo;
    private VisualElement _lbScorersBody, _lbReboundersBody, _lbAssistersBody;
    private Button _btnDashboard;
    private Label _headerSubtitle, _headerGameDay;
    private VisualElement _loadingSpinner;
    private IVisualElementScheduledItem _spinScheduler;
    private bool _isLoading;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;
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

        AudioManager.Instance?.PlayMusic("backgroundGameDay");
        CacheReferences();
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _gamesBody = _root.Q<VisualElement>("GamesBody");
        _mvpPanel = _root.Q<VisualElement>("MvpPanel");
        _mvpName = _root.Q<Label>("MvpName");
        _mvpTeam = _root.Q<Label>("MvpTeam");
        _mvpPos = _root.Q<Label>("MvpPos");
        _mvpPts = _root.Q<Label>("MvpPts");
        _mvpReb = _root.Q<Label>("MvpReb");
        _mvpAst = _root.Q<Label>("MvpAst");
        _mvpVal = _root.Q<Label>("MvpVal");
        _mvpLogo = _root.Q<VisualElement>("MvpLogo");
        _lbScorersBody = _root.Q<VisualElement>("LbScorersBody");
        _lbReboundersBody = _root.Q<VisualElement>("LbReboundersBody");
        _lbAssistersBody = _root.Q<VisualElement>("LbAssistersBody");
        _btnDashboard = _root.Q<Button>("BtnDashboard");
        _headerSubtitle = _root.Q<Label>("HeaderSubtitle");
        _headerGameDay = _root.Q<Label>("HeaderGameDay");
        _loadingSpinner = _root.Q<VisualElement>("LoadingSpinner");
        _loadingSpinner.style.display = DisplayStyle.None;
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            PlayClick();
            GoToDashboard();
        }
    }

    void RegisterCallbacks()
    {
        _btnDashboard?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); GoToDashboard(); });

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
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("MarketSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });

        _root.Q<Button>("SubmenuOfertas")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Market);
        });
        _root.Q<Button>("SubmenuCartera")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Cartera); });
        _root.Q<Button>("SubmenuHistorial")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Historial); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("FinanceSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });
        _root.Q<Button>("SubmenuDecisiones")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Finances);
        });
        _root.Q<Button>("SubmenuPrestamos")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Loans);
        });
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });

        _root.Q<Button>("BtnReset")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.MainMenu); });
    }

    void Refresh()
    {
        RefreshHeader();
        LoadResults();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        SetLogo(_root.Q<VisualElement>("HeaderTeamLogo"), _myTeam.logo, "64x64");

        _root.Q<Label>("HeaderTeamName").text = _myTeam.name.ToUpper();
        _root.Q<Label>("HeaderManagerName").text = $"Manager: {_manager.name}";

        int displayDay = GameResultCache.LastGameDay > 0 ? GameResultCache.LastGameDay : _season?.current_game_day ?? 0;

        if (_season != null)
        {
            _root.Q<Label>("HeaderSeason").text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerGameDay.text = displayDay < 0 ? "AMISTOSO" : $"Jornada {displayDay}";
            var gamesOnDay = DatabaseManager.Instance.GetAllGamesByGameDay(_manager.id, displayDay);
            var firstGame = gamesOnDay.FirstOrDefault();
            _root.Q<Label>("HeaderDate").text = firstGame != null
                ? System.DateTime.Parse(firstGame.game_date).ToString("dd/MM/yyyy")
                : DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }
    }

    void LoadResults()
    {
        if (_season == null) return;

        int gameDay = GameResultCache.LastGameDay > 0 ? GameResultCache.LastGameDay : _season.current_game_day;
        
        var gamesToday = DatabaseManager.Instance.GetAllGamesByGameDay(_manager.id, gameDay)
            .Where(g => g.is_played == 1).ToList();
        _gamesBody.Clear();

        var allPlayerStats = new List<PlayerGameStats>();

        var allStatsBatch = gamesToday.Count > 0
            ? DatabaseManager.Instance.GetGamePlayerStatsBatch(gamesToday.Select(g => g.id).ToList())
            : new List<PlayerGameStats>();

        foreach (var g in gamesToday)
        {
            bool isAllStar = g.game_type == "allstar";
            var home = !isAllStar ? _allTeams.Find(t => t.id == g.home_team_id) : null;
            var away = !isAllStar ? _allTeams.Find(t => t.id == g.away_team_id) : null;
            var homeStats = allStatsBatch.Where(s => s.game_id == g.id && s.team_id == g.home_team_id).ToList();
            var awayStats = allStatsBatch.Where(s => s.game_id == g.id && s.team_id == g.away_team_id).ToList();
            allPlayerStats.AddRange(homeStats);
            allPlayerStats.AddRange(awayStats);

            bool isMyGame = g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id;

            var card = new VisualElement();
            card.AddToClassList("game-card");
            if (isMyGame) card.AddToClassList("my-game");

            var content = new VisualElement();
            content.AddToClassList("game-card-content");

            var homeSide = new VisualElement();
            homeSide.AddToClassList("game-card-team-side");
            homeSide.AddToClassList("team-side-first");
            var homeName = new Label { text = isAllStar ? "CONFERENCIA\nESTE" : (home?.name.ToUpper() ?? "") };
            homeName.AddToClassList("game-card-team-name");
            if (g.home_team_id == _myTeam.id) homeName.AddToClassList("my-team");
            var homeLogo = new VisualElement();
            homeLogo.AddToClassList("game-card-logo");
            SetLogo(homeLogo, isAllStar ? "all-star-game" : home?.logo, "64x64");
            homeSide.Add(homeName);
            homeSide.Add(homeLogo);

            var score = new VisualElement();
            score.AddToClassList("game-card-score");
            var hScoreLbl = new Label { text = g.home_score.ToString() };
            hScoreLbl.AddToClassList("game-card-score-home");
            if (g.home_score > g.away_score)
                hScoreLbl.AddToClassList("winner");
            score.Add(hScoreLbl);

            var sepLbl = new Label { text = "-" };
            sepLbl.AddToClassList("game-card-score-sep");
            score.Add(sepLbl);

            var aScoreLbl = new Label { text = g.away_score.ToString() };
            aScoreLbl.AddToClassList("game-card-score-away");
            if (g.away_score > g.home_score)
                aScoreLbl.AddToClassList("winner");
            score.Add(aScoreLbl);

            var awaySide = new VisualElement();
            awaySide.AddToClassList("game-card-team-side");
            var awayLogo = new VisualElement();
            awayLogo.AddToClassList("game-card-logo");
            SetLogo(awayLogo, isAllStar ? "all-star-game" : away?.logo, "64x64");
            var awayName = new Label { text = isAllStar ? "CONFERENCIA\nOESTE" : (away?.name.ToUpper() ?? "") };
            awayName.AddToClassList("game-card-team-name");
            if (g.away_team_id == _myTeam.id) awayName.AddToClassList("my-team");
            awaySide.Add(awayLogo);
            awaySide.Add(awayName);

            content.Add(homeSide);
            content.Add(score);
            content.Add(awaySide);
            card.Add(content);
            _gamesBody.Add(card);
        }

        _mvpPanel.style.display = DisplayStyle.None;

        if (allPlayerStats.Count == 0) return;

        var mvp = allPlayerStats.OrderByDescending(s => s.rating).First();
        var mvpPlayer = DatabaseManager.Instance.GetPlayerById(mvp.player_id);
        if (mvpPlayer != null)
        {
            _mvpPanel.style.display = DisplayStyle.Flex;
            var mvpTeamData = _allTeams.Find(t => t.id == mvp.team_id)
                ?? _allTeams.Find(t => t.id == mvpPlayer.team_id);
            _mvpName.text = $"{mvpPlayer.first_name.ToUpper()}\n{mvpPlayer.last_name.ToUpper()}";
            _mvpTeam.text = mvpTeamData?.name ?? "Equipo desconocido";
            _mvpPos.text = mvpPlayer.position;
            _mvpPts.text = mvp.points.ToString();
            _mvpReb.text = mvp.rebounds.ToString();
            _mvpAst.text = mvp.assists.ToString();
            _mvpVal.text = mvp.rating.ToString();
            SetLogo(_mvpLogo, mvpTeamData?.logo, "64x64");
        }

        BuildLeaderboard(_lbScorersBody, allPlayerStats.OrderByDescending(s => s.points).Take(3).ToList(), s => s.points);
        BuildLeaderboard(_lbReboundersBody, allPlayerStats.OrderByDescending(s => s.rebounds).Take(3).ToList(), s => s.rebounds);
        BuildLeaderboard(_lbAssistersBody, allPlayerStats.OrderByDescending(s => s.assists).Take(3).ToList(), s => s.assists);
    }

    void BuildLeaderboard(VisualElement body, List<PlayerGameStats> top, System.Func<PlayerGameStats, int> valFn)
    {
        body.Clear();
        for (int i = 0; i < top.Count; i++)
        {
            var s = top[i];
            var player = DatabaseManager.Instance.GetPlayerById(s.player_id);
            if (player == null) continue;
            var team = _allTeams.Find(t => t.id == s.team_id) ?? _allTeams.Find(t => t.id == player.team_id);

            var row = new VisualElement();
            row.AddToClassList("lb-row");
            if (i == top.Count - 1) row.AddToClassList("lb-row--last");
            if (s.team_id == _myTeam.id) row.AddToClassList("my-player");

            var rank = new Label { text = (i + 1).ToString() };
            rank.AddToClassList("lb-rank");
            if (i == 0) rank.AddToClassList("lb-first");

            var logo = new VisualElement();
            logo.AddToClassList("lb-logo");
            SetLogo(logo, team?.logo, "32x32");

            var info = new VisualElement();
            info.AddToClassList("lb-info");
            var nameLbl = new Label { text = $"{player.first_name} {player.last_name}" };
            nameLbl.AddToClassList("lb-name");
            info.Add(nameLbl);
            var teamLbl = new Label { text = $"{team?.abbreviation ?? "FA"} · {player.position}" };
            teamLbl.AddToClassList("lb-team");
            info.Add(teamLbl);

            var val = new Label { text = valFn(s).ToString() };
            val.AddToClassList("lb-value");

            row.Add(rank);
            row.Add(logo);
            row.Add(info);
            row.Add(val);
            body.Add(row);
        }
    }

    void SetLogo(VisualElement elem, string logoName, string sizeFolder = null)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;

        if (!string.IsNullOrEmpty(sizeFolder))
        {
            var sprite = Resources.Load<Sprite>($"Teams/Logos/{sizeFolder}/{logoName}");
            if (sprite != null)
            {
                elem.style.backgroundImage = new StyleBackground(sprite);
                return;
            }
        }

        if (_logoSprites.TryGetValue(logoName, out var fallback))
            elem.style.backgroundImage = new StyleBackground(fallback);
    }

    void GoToDashboard()
    {
        if (_isLoading) return;
        ShowLoading();
        StartCoroutine(NavigateToDashboard());
    }

    IEnumerator NavigateToDashboard()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSecondsRealtime(0.15f);
        HideLoading();
        ScreenManager.Instance.GoTo(GameScreen.Dashboard);
    }

    void ShowLoading()
    {
        if (_isLoading) return;
        _isLoading = true;
        _btnDashboard.SetEnabled(false);
        _loadingSpinner.style.display = DisplayStyle.Flex;

        _spinScheduler = _root.schedule.Execute(() =>
        {
            if (_loadingSpinner == null) return;
            var current = _loadingSpinner.style.rotate;
            float angle = current.value.angle.value + 15f;
            if (angle >= 360f) angle -= 360f;
            _loadingSpinner.style.rotate = new Rotate(Angle.Degrees(angle));
        }).Every(30);
    }

    void HideLoading()
    {
        _spinScheduler?.Pause();
        _spinScheduler = null;
        _loadingSpinner.style.display = DisplayStyle.None;
        _btnDashboard.SetEnabled(true);
        _isLoading = false;
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
