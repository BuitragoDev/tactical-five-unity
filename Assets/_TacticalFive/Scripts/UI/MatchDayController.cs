using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class MatchDayController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Label _homeScore, _awayScore, _homeName, _awayName;
    private VisualElement _homeLogo, _awayLogo;
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

        CursorManager.Instance?.SetDefaultCursor();
        AudioManager.Instance?.PlayMusic("backgroundGameDay");
        CacheReferences();
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
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
            OnContinue();
        }
    }

    void RegisterCallbacks()
    {
        _btnContinue?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnContinue(); });

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
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });

        _root.Q<Button>("BtnReset")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.MainMenu); });

        if (CursorManager.Instance != null)
        {
            var btnNames = new[] {
                "BtnContinue", "NavDashboard", "NavRoster", "NavCalendar",
                "NavStandings", "NavPalmares", "NavResults", "NavPlayoffs",
                "NavStats", "NavRecords", "NavMarket", "NavFinances",
                "NavSponsors", "NavTV", "NavArena", "NavMessages", "BtnReset",
                "SubmenuOfertas", "SubmenuCartera", "SubmenuHistorial",
                "SubmenuDecisiones", "SubmenuPrestamos"
            };
            foreach (var name in btnNames)
            {
                var el = _root.Q<VisualElement>(name);
                if (el != null)
                    CursorManager.Instance.RegisterHandCursor(el);
            }
        }
    }

    void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[MatchDay] RefreshHeader error: {ex.Message}"); }
        LoadMatchData();
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
            SetLogo(_homeLogo, "all-star-game", "80x80");
            SetLogo(_awayLogo, "all-star-game", "80x80");
            _homeName.text = "ESTE";
            _awayName.text = "OESTE";
        }
        else
        {
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

        // Only show players with minutes > 0
        var playingStats = stats.Where(s => s.minutes > 0).ToList();

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
        lbl.AddToClassList(className);
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

        ScreenManager.Instance.GoTo(hasMoreGames ? GameScreen.GameResults : GameScreen.Dashboard);
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

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
