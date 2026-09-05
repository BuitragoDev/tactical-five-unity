using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MatchDayController : UIScreenController
{
    private Label _homeScore, _awayScore, _homeName, _awayName;
    private VisualElement _homeLogo, _awayLogo;
    private string _homeLogoName, _awayLogoName;
    private Label _venueLabel;
    private VisualElement _homeBoxBody, _awayBoxBody;
    private VisualElement _homeBoxFooter, _awayBoxFooter;
    private Label _homeBoxName, _awayBoxName;
    private VisualElement _homeBoxLogo, _awayBoxLogo;
    private Button _btnContinue;

    // My team badges
    private Label _homeMyTeamBadge, _awayMyTeamBadge;

    // Header
    private Label _headerSubtitle, _headerGameDay;

    private List<TeamData> _allTeams;
    private Dictionary<string, Sprite> _logoSprites = new();

    // Play-by-play overlay
    private VisualElement _pbpOverlay;
    private Label _pbpHomeName, _pbpAwayName, _pbpScore, _pbpQuarter, _pbpText;
    private Button _pbpSkip;
    private Button _pbpBtnSpeed1, _pbpBtnSpeed3, _pbpBtnSpeed5, _pbpBtnSpeed10;
    private Coroutine _pbpRoutine;
    private bool _pbpActive;
    private bool _pbpFinished;
    private List<GameSimulator.PlayByPlayEvent> _pbpLog;
    private int _pbpSpeed = 5;
    private const float PBP_BASE_SECONDS = 2f;
    private const string PBP_SPEED_PREF = "TF_PbpSpeed";

    private Label _pbpClock;
    private VisualElement _pbpHomeLogo, _pbpAwayLogo;
    private VisualElement _pbpProgressFill;
    private Label _pbpProgressLabel;
    private VisualElement _pbpHomeBox, _pbpAwayBox;
    private VisualElement _pbpHomeBoxBody, _pbpAwayBoxBody;
    private VisualElement _pbpHomeBoxFooter, _pbpAwayBoxFooter;
    private Dictionary<int, PlayerLiveBox> _pbpPlayers = new();
    private int _pbpGameId, _pbpHomeTeamId, _pbpAwayTeamId;
    private float _pbpTotalMinutes;
    private PanelTotals _homeTotals, _awayTotals;
    private Label _homeTotalPts, _homeTotalTc, _homeTotalTp, _homeTotalTl, _homeTotalReb, _homeTotalAst, _homeTotalStl, _homeTotalBlk, _homeTotalTo, _homeTotalPf;
    private Label _awayTotalPts, _awayTotalTc, _awayTotalTp, _awayTotalTl, _awayTotalReb, _awayTotalAst, _awayTotalStl, _awayTotalBlk, _awayTotalTo, _awayTotalPf;

    private class PlayerLiveBox
    {
        public int teamId;
        public bool isStarter;
        public float minutes;
        public int points, fgm, fga, fg3m, fg3a, ftm, fta, oreb, dreb, assists, steals, blocks, turnovers, pf;
        public int rating => points + oreb + dreb + assists + steals + blocks - (fga - fgm) - (fta - ftm) - turnovers - pf;
        public int rebounds => oreb + dreb;

        public Label minLbl, ptsLbl, tcLbl, tpLbl, tlLbl, rebLbl, astLbl, stlLbl, blkLbl, toLbl, pfLbl, valLbl;

        public void ApplyDelta(string stat, float amount)
        {
            switch (stat)
            {
                case "min": minutes += amount; break;
                case "pts": points += (int)amount; break;
                case "fgm": fgm += (int)amount; break;
                case "fga": fga += (int)amount; break;
                case "fg3m": fg3m += (int)amount; break;
                case "fg3a": fg3a += (int)amount; break;
                case "ftm": ftm += (int)amount; break;
                case "fta": fta += (int)amount; break;
                case "oreb": oreb += (int)amount; break;
                case "dreb": dreb += (int)amount; break;
                case "ast": assists += (int)amount; break;
                case "stl": steals += (int)amount; break;
                case "blk": blocks += (int)amount; break;
                case "to": turnovers += (int)amount; break;
                case "pf": pf += (int)amount; break;
            }
        }

        public void UpdateLabels()
        {
            if (minLbl != null) minLbl.text = $"{Mathf.RoundToInt(minutes)}";
            if (ptsLbl != null) ptsLbl.text = $"{points}";
            if (tcLbl != null) tcLbl.text = $"{fgm}/{fga}";
            if (tpLbl != null) tpLbl.text = $"{fg3m}/{fg3a}";
            if (tlLbl != null) tlLbl.text = $"{ftm}/{fta}";
            if (rebLbl != null) rebLbl.text = $"{rebounds}";
            if (astLbl != null) astLbl.text = $"{assists}";
            if (stlLbl != null) stlLbl.text = $"{steals}";
            if (blkLbl != null) blkLbl.text = $"{blocks}";
            if (toLbl != null) toLbl.text = $"{turnovers}";
            if (pfLbl != null) pfLbl.text = $"{pf}";
            if (valLbl != null) valLbl.text = $"{rating}";
        }
    }

    struct PanelTotals
    {
        public int fgm, fga, fg3m, fg3a, ftm, fta, oreb, dreb, assists, steals, blocks, turnovers, pf;
        public int points => (fgm - fg3m) * 2 + fg3m * 3 + ftm;
        public int rebounds => oreb + dreb;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CursorManager.Instance?.SetDefaultCursor();
        AudioManager.Instance?.PlayMusic("backgroundGameDay");
    }

    protected override void CacheReferences()
    {
        _homeScore = _root.Q<Label>("HomeScore");
        _awayScore = _root.Q<Label>("AwayScore");
        _homeName = _root.Q<Label>("HomeName");
        _awayName = _root.Q<Label>("AwayName");
        _homeLogo = _root.Q<VisualElement>("HomeLogo");
        _awayLogo = _root.Q<VisualElement>("AwayLogo");
        _venueLabel = _root.Q<Label>("VenueLabel");
        _homeBoxBody = _root.Q<VisualElement>("HomeBoxBody");
        _awayBoxBody = _root.Q<VisualElement>("AwayBoxBody");
        _homeBoxFooter = _root.Q<VisualElement>("HomeBoxFooter");
        _awayBoxFooter = _root.Q<VisualElement>("AwayBoxFooter");
        _homeBoxName = _root.Q<Label>("HomeBoxName");
        _awayBoxName = _root.Q<Label>("AwayBoxName");
        _homeBoxLogo = _root.Q<VisualElement>("HomeBoxLogo");
        _awayBoxLogo = _root.Q<VisualElement>("AwayBoxLogo");
        _btnContinue = _root.Q<Button>("BtnContinue");

        // Badges
        _homeMyTeamBadge = _root.Q<Label>("HomeMyTeamBadge");
        _awayMyTeamBadge = _root.Q<Label>("AwayMyTeamBadge");

        // Header
        _headerSubtitle = _root.Q<Label>("HeaderSubtitle");
        _headerGameDay = _root.Q<Label>("HeaderGameDay");

        // Play-by-play overlay
        _pbpOverlay = _root.Q<VisualElement>("PlayByPlayOverlay");
        _pbpHomeName = _root.Q<Label>("PbpHomeName");
        _pbpAwayName = _root.Q<Label>("PbpAwayName");
        _pbpScore = _root.Q<Label>("PbpScore");
        _pbpQuarter = _root.Q<Label>("PbpQuarter");
        _pbpText = _root.Q<Label>("PbpText");
        _pbpSkip = _root.Q<Button>("PbpSkip");
        _pbpBtnSpeed1 = _root.Q<Button>("PbpBtnSpeed1");
        _pbpBtnSpeed3 = _root.Q<Button>("PbpBtnSpeed3");
        _pbpBtnSpeed5 = _root.Q<Button>("PbpBtnSpeed5");
        _pbpBtnSpeed10 = _root.Q<Button>("PbpBtnSpeed10");
        _pbpClock = _root.Q<Label>("PbpClock");
        _pbpHomeLogo = _root.Q<VisualElement>("PbpHomeLogo");
        _pbpAwayLogo = _root.Q<VisualElement>("PbpAwayLogo");
        _pbpProgressFill = _root.Q<VisualElement>("PbpProgressFill");
        _pbpProgressLabel = _root.Q<Label>("PbpProgressLabel");
        _pbpHomeBox = _root.Q<VisualElement>("PbpHomeBox");
        _pbpAwayBox = _root.Q<VisualElement>("PbpAwayBox");

        _pbpSpeed = PlayerPrefs.GetInt(PBP_SPEED_PREF, 5);
        UpdatePbpSpeedButtons();
    }

    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _allTeams = DatabaseManager.Instance.GetAllTeams();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            PlayClick();
            if (_pbpActive) { if (!_pbpFinished) JumpToPbpEnd(); return; }
            OnContinue();
        }
    }

    protected override void RegisterCallbacks()
    {
        _btnContinue?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnContinue(); });
        _pbpSkip?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnPbpSkip(); });
        _pbpBtnSpeed1?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectPbpSpeed(1); });
        _pbpBtnSpeed3?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectPbpSpeed(3); });
        _pbpBtnSpeed5?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectPbpSpeed(5); });
        _pbpBtnSpeed10?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectPbpSpeed(10); });

        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Roster); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavCompeticiones")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("CompetitionsSubmenu");
            if (submenu == null) return;
            submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });
        _root.Q<Button>("SubmenuResultados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("CompetitionsSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("SubmenuClasificacion")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("CompetitionsSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("SubmenuInfoLiga")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("CompetitionsSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.InfoLeague); });
        _root.Q<Button>("SubmenuBuscador")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Buscador); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Palmares); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Records); });
        var allSubmenus = new[] {
            _root.Q<VisualElement>("MarketSubmenu"),
            _root.Q<VisualElement>("FinanceSubmenu")
        };

        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("MarketSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
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
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
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
        _root.Q<Button>("NavCity")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("CitySubmenu");
            if (submenu == null) return;
            submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });
        _root.Q<Button>("SubmenuVerCiudad")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("CitySubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.TheCity); });
        _root.Q<Button>("SubmenuPabellon")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("CitySubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavManager")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Manager); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });

        _root.Q<Button>("BtnReset")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.MainMenu); });

        if (CursorManager.Instance != null)
        {
            var btnNames = new[] {
                "BtnContinue", "PbpSkip", "PbpBtnSpeed1", "PbpBtnSpeed3", "PbpBtnSpeed5", "PbpBtnSpeed10",
                "NavDashboard", "NavRoster", "NavCalendar",
                "NavCompeticiones", "NavPalmares", "NavPlayoffs",
                "NavStats", "NavRecords", "NavMarket", "NavFinances",
                "NavSponsors", "NavTV", "NavCity", "NavManager", "NavMessages", "BtnReset",
                "SubmenuResultados", "SubmenuClasificacion", "SubmenuInfoLiga",
                "SubmenuBuscador",
                "SubmenuOfertas", "SubmenuCartera", "SubmenuHistorial",
                "SubmenuDecisiones", "SubmenuPrestamos",
                "SubmenuVerCiudad", "SubmenuPabellon",
            };
            foreach (var name in btnNames)
            {
                var el = _root.Q<VisualElement>(name);
                if (el != null)
                    CursorManager.Instance.RegisterHandCursor(el);
            }
        }
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[MatchDay] RefreshHeader error: {ex.Message}"); }
        LoadMatchData();
        TryPlayByPlay();
    }

    protected override void RefreshHeader()
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

    void LoadMatchData()
    {
        if (_season == null || _myTeam == null) return;

        int gameDay = GameResultCache.LastGameDay > 0 ? GameResultCache.LastGameDay : _season.current_game_day;
        
        var gamesToday = DatabaseManager.Instance.GetAllGamesByGameDay(_manager.id, gameDay);
        var myGame = gamesToday.FirstOrDefault(g =>
            g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id);
        if (myGame == null)
            myGame = gamesToday.FirstOrDefault(g => g.game_type == "allstar");
        if (myGame == null) return;

        bool isAllStar = myGame.game_type == "allstar";
        var home = !isAllStar ? _allTeams.Find(t => t.id == myGame.home_team_id) : null;
        var away = !isAllStar ? _allTeams.Find(t => t.id == myGame.away_team_id) : null;

        // Banner
        if (isAllStar)
        {
            _homeLogoName = "all-star-game";
            _awayLogoName = "all-star-game";
            SetLogo(_homeLogo, "all-star-game", "80x80");
            SetLogo(_awayLogo, "all-star-game", "80x80");
            _homeName.text = "ESTE";
            _awayName.text = "OESTE";
        }
        else
        {
            _homeLogoName = home?.logo;
            _awayLogoName = away?.logo;
            SetLogo(_homeLogo, home?.logo, "80x80");
            SetLogo(_awayLogo, away?.logo, "80x80");
            _homeName.text = home?.name.ToUpper() ?? "";
            _awayName.text = away?.name.ToUpper() ?? "";
        }
        _homeScore.text = myGame.home_score.ToString();
        _awayScore.text = myGame.away_score.ToString();

        // Venue with attendance
        if (isAllStar)
        {
            _venueLabel.text = "All-Star Arena (50,000 espectadores)";
        }
        else
        {
            var attendance = DatabaseManager.Instance.GetGameAttendance(myGame.id);
            string arenaName = home?.arena ?? "Pabellón";
            int attendanceCount = attendance?.attendance ?? 0;

            if (attendanceCount == 0 && home != null)
            {
                bool myTeamIsHome = myGame.home_team_id == _myTeam.id;
                bool myTeamIsAway = myGame.away_team_id == _myTeam.id;

                var teamGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
                var homeTeamGames = teamGames.Where(g => g.home_team_id == home.id || g.away_team_id == home.id).ToList();
                int wins = homeTeamGames.Count(g =>
                    (g.home_team_id == home.id && g.home_score > g.away_score) ||
                    (g.away_team_id == home.id && g.away_score > g.home_score));
                int totalPlayed = homeTeamGames.Count;
                float winPct = totalPlayed > 0 ? (float)wins / totalPlayed : 0.5f;

                float baseAttendance;
                float randomFactor = 0.92f + UnityEngine.Random.value * 0.16f;

                if (myTeamIsHome)
                {
                    var rival = DatabaseManager.Instance.GetTeamById(myGame.away_team_id);
                    float rivalRepFactor = rival != null ? (rival.reputation / 5f) * 0.08f : 0f;
                    baseAttendance = home.capacity * (
                        0.30f +
                        (_manager.fan_confidence / 100f) * 0.35f +
                        winPct * 0.15f +
                        rivalRepFactor
                    );
                }
                else if (myTeamIsAway)
                {
                    float myRepFactor = (_myTeam.reputation / 5f) * 0.06f;
                    baseAttendance = home.capacity * (
                        0.55f +
                        winPct * 0.30f +
                        myRepFactor
                    );
                }
                else
                {
                    baseAttendance = home.capacity * (0.55f + winPct * 0.40f);
                }

                attendanceCount = (int)Mathf.Min(home.capacity, baseAttendance * randomFactor);
            }

            string attendanceText = attendanceCount > 0
                ? $" ({attendanceCount:N0} espectadores)"
                : "";
            _venueLabel.text = $"{arenaName}{attendanceText}";
        }

        // My team badges
        if (isAllStar)
        {
            _homeMyTeamBadge.style.display = DisplayStyle.None;
            _awayMyTeamBadge.style.display = DisplayStyle.None;
        }
        else
        {
            bool homeIsMyTeam = myGame.home_team_id == _myTeam.id;
            _homeMyTeamBadge.style.display = homeIsMyTeam ? DisplayStyle.Flex : DisplayStyle.None;
            _awayMyTeamBadge.style.display = !homeIsMyTeam ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Highlight my team name in gold
        var homeTeamBlock = _root.Q<VisualElement>("HomeTeamBlock");
        var awayTeamBlock = _root.Q<VisualElement>("AwayTeamBlock");
        if (!isAllStar)
        {
            bool homeIsMyTeam = myGame.home_team_id == _myTeam.id;
            if (homeIsMyTeam)
            {
                homeTeamBlock.AddToClassList("my-team");
                awayTeamBlock.RemoveFromClassList("my-team");
            }
            else
            {
                awayTeamBlock.AddToClassList("my-team");
                homeTeamBlock.RemoveFromClassList("my-team");
            }
        }
        else
        {
            homeTeamBlock.RemoveFromClassList("my-team");
            awayTeamBlock.RemoveFromClassList("my-team");
        }

        // Box headers
        if (isAllStar)
        {
            _homeBoxName.text = "ESTE";
            _awayBoxName.text = "OESTE";
            SetLogo(_homeBoxLogo, "all-star-game", "64x64");
            SetLogo(_awayBoxLogo, "all-star-game", "64x64");
        }
        else
        {
            _homeBoxName.text = home?.name.ToUpper() ?? "";
            _awayBoxName.text = away?.name.ToUpper() ?? "";
            SetLogo(_homeBoxLogo, home?.logo, "64x64");
            SetLogo(_awayBoxLogo, away?.logo, "64x64");
        }

        // Player stats - only players with minutes > 0
        var homeStats = DatabaseManager.Instance.GetGamePlayerStats(myGame.id)
            .Where(s => s.team_id == myGame.home_team_id)
            .OrderByDescending(s => s.rating)
            .ToList();
        var awayStats = DatabaseManager.Instance.GetGamePlayerStats(myGame.id)
            .Where(s => s.team_id == myGame.away_team_id)
            .OrderByDescending(s => s.rating)
            .ToList();

        // MVP = player with highest rating across both teams
        var allStats = homeStats.Concat(awayStats).ToList();
        var mvp = allStats.OrderByDescending(s => s.rating).FirstOrDefault();
        int mvpPlayerId = mvp?.player_id ?? -1;

        var starters = GameResultCache.GameStarters.GetValueOrDefault(myGame.id) ?? new HashSet<int>();
        BuildBoxTable(_homeBoxBody, _homeBoxFooter, homeStats, true, myGame, mvpPlayerId, starters);
        BuildBoxTable(_awayBoxBody, _awayBoxFooter, awayStats, false, myGame, mvpPlayerId, starters);
    }


    void BuildBoxTable(VisualElement body, VisualElement footer, List<PlayerGameStats> stats, bool isHome, GameData game, int mvpPlayerId, HashSet<int> starters)
    {
        body.Clear();

        // Show all active players (including those with 0 min)
        var playingStats = stats;

        foreach (var s in playingStats)
        {
            var row = new VisualElement();
            row.AddToClassList("box-row");
            bool isMyTeam = isHome
                ? game.home_team_id == _myTeam.id
                : game.away_team_id == _myTeam.id;
            if (isMyTeam) row.AddToClassList("my-player");
            if (starters.Contains(s.player_id))
                row.AddToClassList("box-row--starter");

            var player = DatabaseManager.Instance.GetPlayerById(s.player_id);
            var playerContainer = new VisualElement();
            playerContainer.AddToClassList("col-player");

            // MVP star
            if (s.player_id == mvpPlayerId)
            {
                var starLbl = new Label { text = "★" };
                starLbl.AddToClassList("mvp-star");
                playerContainer.Add(starLbl);
            }

            // Position badge
            var posLbl = new Label { text = PositionCodes.GetShort(player?.position) };
            posLbl.AddToClassList("player-pos");
            playerContainer.Add(posLbl);

            // Player name
            var nameLbl = new Label { text = player != null ? $"{player.first_name} {player.last_name}" : "???" };
            nameLbl.AddToClassList("player-name");
            playerContainer.Add(nameLbl);

            row.Add(playerContainer);

            row.Add(MakeStatLabel(s.minutes.ToString("F0"), "col-min"));
            row.Add(MakeStatLabel(s.points.ToString(), "col-pts"));
            row.Add(MakeStatLabel($"{s.fgm}/{s.fga}", "col-fg", 50));
            row.Add(MakeStatLabel($"{s.fg3m}/{s.fg3a}", "col-3p", 46));
            row.Add(MakeStatLabel($"{s.ftm}/{s.fta}", "col-ft", 44));
            row.Add(MakeStatLabel(s.rebounds.ToString(), "col-reb"));
            row.Add(MakeStatLabel(s.assists.ToString(), "col-ast"));
            row.Add(MakeStatLabel(s.steals.ToString(), "col-stl"));
            row.Add(MakeStatLabel(s.blocks.ToString(), "col-blk"));
            row.Add(MakeStatLabel(s.turnovers.ToString(), "col-to"));
            row.Add(MakeStatLabel(s.pf.ToString(), "col-pf"));
            row.Add(MakeStatLabel(s.rating.ToString(), "col-val"));

            body.Add(row);
        }

        footer.Clear();
        var totalRow = new VisualElement();
        totalRow.AddToClassList("box-total-row");

        var totalPlayerLbl = new Label { text = "Totales" };
        totalPlayerLbl.AddToClassList("col-player");
        totalRow.Add(totalPlayerLbl);

        totalRow.Add(MakeStatLabel("", "col-min"));
        totalRow.Add(MakeStatLabel(isHome ? game.home_score.ToString() : game.away_score.ToString(), "col-pts"));
        totalRow.Add(MakeStatLabel($"{playingStats.Sum(s => s.fgm)}/{playingStats.Sum(s => s.fga)}", "col-fg", 50));
        totalRow.Add(MakeStatLabel($"{playingStats.Sum(s => s.fg3m)}/{playingStats.Sum(s => s.fg3a)}", "col-3p", 46));
        totalRow.Add(MakeStatLabel($"{playingStats.Sum(s => s.ftm)}/{playingStats.Sum(s => s.fta)}", "col-ft", 44));
        totalRow.Add(MakeStatLabel(playingStats.Sum(s => s.rebounds).ToString(), "col-reb"));
        totalRow.Add(MakeStatLabel(playingStats.Sum(s => s.assists).ToString(), "col-ast"));
        totalRow.Add(MakeStatLabel(playingStats.Sum(s => s.steals).ToString(), "col-stl"));
        totalRow.Add(MakeStatLabel(playingStats.Sum(s => s.blocks).ToString(), "col-blk"));
        totalRow.Add(MakeStatLabel(playingStats.Sum(s => s.turnovers).ToString(), "col-to"));
        totalRow.Add(MakeStatLabel(playingStats.Sum(s => s.pf).ToString(), "col-pf"));
        totalRow.Add(MakeStatLabel("", "col-val"));
        footer.Add(totalRow);
    }

    Label MakeStatLabel(string text, string className, float width = 36)
    {
        var lbl = new Label { text = text };
        foreach (var cls in className.Split(' '))
            lbl.AddToClassList(cls);
        lbl.style.width = width;
        lbl.style.flexShrink = 0;
        return lbl;
    }

    void OnContinue()
    {
        if (_season == null) { ScreenManager.Instance.GoTo(GameScreen.Dashboard); return; }

        int gameDay = GameResultCache.LastGameDay > 0 ? GameResultCache.LastGameDay : _season.current_game_day;
        var simulatedIds = GameResultCache.SimulatedGameIds;
        var myGame = DatabaseManager.Instance.GetAllGamesByGameDay(_manager.id, gameDay)
            .FirstOrDefault(g => g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id);
        int myGameId = myGame?.id ?? 0;

        // Check if there are other games on the same day (excluding my team's game)
        bool hasMoreGames = simulatedIds.Any(id => id != myGameId);

        ScreenManager.Instance.GoTo(GameScreen.GameResults);
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

    void TryPlayByPlay()
    {
        if (UIScreenController.GetSimMode() != 1) return;
        if (_season == null || _myTeam == null) return;

        int gameDay = GameResultCache.LastGameDay > 0 ? GameResultCache.LastGameDay : _season.current_game_day;
        var gamesToday = DatabaseManager.Instance.GetAllGamesByGameDay(_manager.id, gameDay);
        var myGame = gamesToday.FirstOrDefault(g =>
            g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id);
        if (myGame == null)
            myGame = gamesToday.FirstOrDefault(g => g.game_type == "allstar");
        if (myGame == null) return;

        if (!GameResultCache.PlayByPlayLogs.TryGetValue(myGame.id, out var log) || log == null || log.Count == 0) return;

        _pbpGameId = myGame.id;
        _pbpHomeTeamId = myGame.home_team_id;
        _pbpAwayTeamId = myGame.away_team_id;
        _pbpTotalMinutes = 48 + Mathf.Max(0, log.Max(e => e.quarter) - 4) * 5;
        BuildPbpBoxscore();
        StartPlayByPlay(log);
    }

    void StartPlayByPlay(List<GameSimulator.PlayByPlayEvent> log)
    {
        _pbpActive = true;
        _pbpFinished = false;
        _pbpSkip.text = "SALTAR ►";
        _pbpHomeName.text = _homeName.text;
        _pbpAwayName.text = _awayName.text;
        SetLogo(_pbpHomeLogo, _homeLogoName, "120x120");
        SetLogo(_pbpAwayLogo, _awayLogoName, "120x120");
        _pbpOverlay.style.display = DisplayStyle.Flex;
        if (_pbpRoutine != null) StopCoroutine(_pbpRoutine);
        _pbpRoutine = StartCoroutine(PlayByPlayCoroutine(log));
    }

    void StopPlayByPlay()
    {
        _pbpActive = false;
        if (_pbpRoutine != null) { StopCoroutine(_pbpRoutine); _pbpRoutine = null; }
        _pbpOverlay.style.display = DisplayStyle.None;
    }

    void ApplyPbpDeltas(List<GameSimulator.StatDelta> deltas)
    {
        if (deltas == null) return;
        var changed = new HashSet<int>();
        foreach (var d in deltas)
        {
            if (_pbpPlayers.TryGetValue(d.player_id, out var box))
            {
                box.ApplyDelta(d.stat, d.amount);
                changed.Add(d.player_id);
            }
        }
        foreach (var pid in changed)
        {
            if (_pbpPlayers.TryGetValue(pid, out var box))
                box.UpdateLabels();
        }
        RecalcTotals();
        UpdateTotalRow(_pbpHomeBoxFooter, ref _homeTotals, _homeTotalPts, _homeTotalTc, _homeTotalTp, _homeTotalTl, _homeTotalReb, _homeTotalAst, _homeTotalStl, _homeTotalBlk, _homeTotalTo, _homeTotalPf);
        UpdateTotalRow(_pbpAwayBoxFooter, ref _awayTotals, _awayTotalPts, _awayTotalTc, _awayTotalTp, _awayTotalTl, _awayTotalReb, _awayTotalAst, _awayTotalStl, _awayTotalBlk, _awayTotalTo, _awayTotalPf);
        SortBodyByVal(_pbpHomeBoxBody);
        SortBodyByVal(_pbpAwayBoxBody);
    }

    void UpdateTotalRow(VisualElement footer, ref PanelTotals t, Label pts, Label tc, Label tp, Label tl, Label reb, Label ast, Label stl, Label blk, Label to_, Label pf)
    {
        if (pts != null) pts.text = $"{t.points}";
        if (tc != null) tc.text = $"{t.fgm}/{t.fga}";
        if (tp != null) tp.text = $"{t.fg3m}/{t.fg3a}";
        if (tl != null) tl.text = $"{t.ftm}/{t.fta}";
        if (reb != null) reb.text = $"{t.rebounds}";
        if (ast != null) ast.text = $"{t.assists}";
        if (stl != null) stl.text = $"{t.steals}";
        if (blk != null) blk.text = $"{t.blocks}";
        if (to_ != null) to_.text = $"{t.turnovers}";
        if (pf != null) pf.text = $"{t.pf}";
    }

    void RecalcTotals()
    {
        _homeTotals = default; _awayTotals = default;
        foreach (var box in _pbpPlayers.Values)
        {
            ref var t = ref (box.teamId == _pbpHomeTeamId ? ref _homeTotals : ref _awayTotals);
            t.fgm += box.fgm; t.fga += box.fga;
            t.fg3m += box.fg3m; t.fg3a += box.fg3a;
            t.ftm += box.ftm; t.fta += box.fta;
            t.oreb += box.oreb; t.dreb += box.dreb;
            t.assists += box.assists;
            t.steals += box.steals;
            t.blocks += box.blocks;
            t.turnovers += box.turnovers;
            t.pf += box.pf;
        }
    }

    void OnPbpSkip()
    {
        if (_pbpFinished) { StopPlayByPlay(); return; }
        JumpToPbpEnd();
    }

    void JumpToPbpEnd()
    {
        if (_pbpRoutine != null) { StopCoroutine(_pbpRoutine); _pbpRoutine = null; }
        _pbpPlayers.Clear(); _homeTotals = default; _awayTotals = default;
        BuildPbpBoxscore();
        if (_pbpLog != null)
        {
            foreach (var ev in _pbpLog)
                ApplyPbpDeltas(ev.deltas);
            var finalEv = _pbpLog[_pbpLog.Count - 1];
            _pbpScore.text = $"{finalEv.homeScore} - {finalEv.awayScore}";
            _pbpProgressFill.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
            if (_pbpProgressLabel != null) _pbpProgressLabel.text = "100%";
            _pbpText.text = "FIN DEL PARTIDO";
            _pbpQuarter.text = finalEv.quarter > 4 ? "PRÓRROGA" : "FINAL";
            float qLen = finalEv.quarter <= 4 ? 12f : 5f;
            float remaining = Mathf.Max(0, qLen - finalEv.timeElapsed);
            int mm = Mathf.FloorToInt(remaining);
            int ss = Mathf.RoundToInt((remaining - mm) * 60f);
            if (ss >= 60) { mm++; ss = 0; }
            _pbpClock.text = $"{mm:D2}:{ss:D2}";
        }
        OnPbpComplete();
    }

    void OnPbpComplete()
    {
        _pbpFinished = true;
        _pbpSkip.text = "IR AL RESUMEN ►";
        _pbpProgressFill.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
        if (_pbpProgressLabel != null) _pbpProgressLabel.text = "100%";
        _pbpText.text = "FIN DEL PARTIDO";
    }

    void SortBodyByVal(VisualElement body)
    {
        if (body == null) return;
        var children = new List<VisualElement>();
        foreach (var child in body.Children())
            children.Add(child);
        if (children.Count < 2) return;
        children.Sort((a, b) => ((b.userData as PlayerLiveBox)?.rating ?? 0).CompareTo((a.userData as PlayerLiveBox)?.rating ?? 0));
        body.Clear();
        foreach (var c in children) body.Add(c);
    }

    System.Collections.IEnumerator PlayByPlayCoroutine(List<GameSimulator.PlayByPlayEvent> log)
    {
        _pbpLog = log;
        for (int i = 0; i < log.Count; i++)
        {
            var ev = log[i];
            _pbpScore.text = $"{ev.homeScore} - {ev.awayScore}";
            _pbpQuarter.text = ev.quarter > 4 ? "PRÓRROGA" : $"CUARTO {ev.quarter}";
            float qLen = ev.quarter <= 4 ? 12f : 5f;
            float remaining = Mathf.Max(0, qLen - ev.timeElapsed);
            int mm = Mathf.FloorToInt(remaining);
            int ss = Mathf.RoundToInt((remaining - mm) * 60f);
            if (ss >= 60) { mm++; ss = 0; }
            _pbpClock.text = $"{mm:D2}:{ss:D2}";

            float cumulative = ev.quarter <= 4
                ? (ev.quarter - 1) * 12f + ev.timeElapsed
                : 48f + (ev.quarter - 5) * 5f + ev.timeElapsed;
            float pct = _pbpTotalMinutes > 0 ? Mathf.Min(1f, cumulative / _pbpTotalMinutes) * 100f : 0f;
            _pbpProgressFill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
            if (_pbpProgressLabel != null)
                _pbpProgressLabel.text = $"{Mathf.RoundToInt(pct)}%";

            _pbpText.text = ev.text;
            ApplyPbpDeltas(ev.deltas);
            yield return new WaitForSecondsRealtime(PBP_BASE_SECONDS / _pbpSpeed);
        }
        OnPbpComplete();
    }

    void BuildPbpBoxscore()
    {
        if (_pbpGameId == 0) return;
        _pbpPlayers.Clear();
        _homeTotals = default; _awayTotals = default;

        var starters = GameResultCache.GameStarters.GetValueOrDefault(_pbpGameId) ?? new HashSet<int>();
        var allStats = DatabaseManager.Instance.GetGamePlayerStats(_pbpGameId);

        var homeStats = allStats.Where(s => s.team_id == _pbpHomeTeamId).OrderByDescending(s => s.rating).ToList();
        var awayStats = allStats.Where(s => s.team_id == _pbpAwayTeamId).OrderByDescending(s => s.rating).ToList();

        _pbpHomeBox.Clear(); _pbpAwayBox.Clear();
        _pbpHomeBoxBody = MakeBoxTableHeader(_pbpHomeBox, _homeBoxName.text);
        _pbpAwayBoxBody = MakeBoxTableHeader(_pbpAwayBox, _awayBoxName.text);

        _homeTotalPts = null; _awayTotalPts = null;
        BuildPbpPanel(_pbpHomeBoxBody, homeStats, _pbpHomeTeamId, starters, out _pbpHomeBoxFooter, out _homeTotalPts, out _homeTotalTc, out _homeTotalTp, out _homeTotalTl, out _homeTotalReb, out _homeTotalAst, out _homeTotalStl, out _homeTotalBlk, out _homeTotalTo, out _homeTotalPf);
        BuildPbpPanel(_pbpAwayBoxBody, awayStats, _pbpAwayTeamId, starters, out _pbpAwayBoxFooter, out _awayTotalPts, out _awayTotalTc, out _awayTotalTp, out _awayTotalTl, out _awayTotalReb, out _awayTotalAst, out _awayTotalStl, out _awayTotalBlk, out _awayTotalTo, out _awayTotalPf);
    }

    VisualElement MakeBoxTableHeader(VisualElement panel, string teamName)
    {
        var header = new VisualElement();
        header.AddToClassList("box-table-header");
        var teamLbl = new Label { text = teamName };
        teamLbl.AddToClassList("col-player");
        teamLbl.AddToClassList("col-stat--header");
        header.Add(teamLbl);
        header.Add(MakeStatLabel("MIN", "col-min col-stat--header"));
        header.Add(MakeStatLabel("PTS", "col-pts col-stat--header"));
        header.Add(MakeStatLabel("TC", "col-fg col-stat--header", 50));
        header.Add(MakeStatLabel("3P", "col-3p col-stat--header", 46));
        header.Add(MakeStatLabel("TL", "col-ft col-stat--header", 44));
        header.Add(MakeStatLabel("REB", "col-reb col-stat--header"));
        header.Add(MakeStatLabel("AST", "col-ast col-stat--header"));
        header.Add(MakeStatLabel("ROB", "col-stl col-stat--header"));
        header.Add(MakeStatLabel("TAP", "col-blk col-stat--header"));
        header.Add(MakeStatLabel("TO", "col-to col-stat--header"));
        header.Add(MakeStatLabel("FP", "col-pf col-stat--header"));
        header.Add(MakeStatLabel("VAL", "col-val col-stat--header"));
        panel.Add(header);

        var body = new VisualElement();
        body.AddToClassList("box-table-body");
        panel.Add(body);
        return body;
    }

    void BuildPbpPanel(VisualElement body, List<PlayerGameStats> stats, int teamId, HashSet<int> starters,
        out VisualElement footer, out Label totalPts, out Label totalTc, out Label totalTp, out Label totalTl,
        out Label totalReb, out Label totalAst, out Label totalStl, out Label totalBlk, out Label totalTo, out Label totalPf)
    {
        foreach (var s in stats)
        {
            var player = DatabaseManager.Instance.GetPlayerById(s.player_id);
            var row = new VisualElement();
            row.AddToClassList("box-row");
            if (starters.Contains(s.player_id))
                row.AddToClassList("box-row--starter");

            var playerContainer = new VisualElement();
            playerContainer.AddToClassList("col-player");
            var posLbl = new Label { text = PositionCodes.GetShort(player?.position) };
            posLbl.AddToClassList("player-pos");
            playerContainer.Add(posLbl);
            var nameLbl = new Label { text = player != null ? $"{player.first_name} {player.last_name}" : "???" };
            nameLbl.AddToClassList("player-name");
            playerContainer.Add(nameLbl);
            row.Add(playerContainer);

            var box = new PlayerLiveBox { teamId = teamId, isStarter = starters.Contains(s.player_id) };
            row.userData = box;
            box.minLbl = MakeStatLabel("0", "col-min"); row.Add(box.minLbl);
            box.ptsLbl = MakeStatLabel("0", "col-pts"); row.Add(box.ptsLbl);
            box.tcLbl = MakeStatLabel("0/0", "col-fg", 50); row.Add(box.tcLbl);
            box.tpLbl = MakeStatLabel("0/0", "col-3p", 46); row.Add(box.tpLbl);
            box.tlLbl = MakeStatLabel("0/0", "col-ft", 44); row.Add(box.tlLbl);
            box.rebLbl = MakeStatLabel("0", "col-reb"); row.Add(box.rebLbl);
            box.astLbl = MakeStatLabel("0", "col-ast"); row.Add(box.astLbl);
            box.stlLbl = MakeStatLabel("0", "col-stl"); row.Add(box.stlLbl);
            box.blkLbl = MakeStatLabel("0", "col-blk"); row.Add(box.blkLbl);
            box.toLbl = MakeStatLabel("0", "col-to"); row.Add(box.toLbl);
            box.pfLbl = MakeStatLabel("0", "col-pf"); row.Add(box.pfLbl);
            box.valLbl = MakeStatLabel("0", "col-val"); row.Add(box.valLbl);
            box.UpdateLabels();

            body.Add(row);
            _pbpPlayers[s.player_id] = box;
        }

        footer = new VisualElement();
        footer.AddToClassList("box-table-footer");
        var totalRow = new VisualElement();
        totalRow.AddToClassList("box-total-row");
        var totalLbl = new Label { text = "Totales" };
        totalLbl.AddToClassList("col-player");
        totalRow.Add(totalLbl);
        totalRow.Add(MakeStatLabel("", "col-min"));
        totalPts = MakeStatLabel("0", "col-pts"); totalRow.Add(totalPts);
        totalTc = MakeStatLabel("0/0", "col-fg", 50); totalRow.Add(totalTc);
        totalTp = MakeStatLabel("0/0", "col-3p", 46); totalRow.Add(totalTp);
        totalTl = MakeStatLabel("0/0", "col-ft", 44); totalRow.Add(totalTl);
        totalReb = MakeStatLabel("0", "col-reb"); totalRow.Add(totalReb);
        totalAst = MakeStatLabel("0", "col-ast"); totalRow.Add(totalAst);
        totalStl = MakeStatLabel("0", "col-stl"); totalRow.Add(totalStl);
        totalBlk = MakeStatLabel("0", "col-blk"); totalRow.Add(totalBlk);
        totalTo = MakeStatLabel("0", "col-to"); totalRow.Add(totalTo);
        totalPf = MakeStatLabel("0", "col-pf"); totalRow.Add(totalPf);
        totalRow.Add(MakeStatLabel("", "col-val"));
        var panel = body.parent;
        panel.Add(footer);
        footer.Add(totalRow);
    }

    void SelectPbpSpeed(int speed)
    {
        _pbpSpeed = speed;
        PlayerPrefs.SetInt(PBP_SPEED_PREF, speed);
        PlayerPrefs.Save();
        UpdatePbpSpeedButtons();
    }

    void UpdatePbpSpeedButtons()
    {
        _pbpBtnSpeed1?.EnableInClassList("pbp-speed-btn--active", _pbpSpeed == 1);
        _pbpBtnSpeed3?.EnableInClassList("pbp-speed-btn--active", _pbpSpeed == 3);
        _pbpBtnSpeed5?.EnableInClassList("pbp-speed-btn--active", _pbpSpeed == 5);
        _pbpBtnSpeed10?.EnableInClassList("pbp-speed-btn--active", _pbpSpeed == 10);
    }
}
