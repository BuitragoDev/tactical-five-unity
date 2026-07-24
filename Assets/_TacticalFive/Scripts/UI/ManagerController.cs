using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class ManagerController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;
    private List<GameData> _standingGames;

    // Stats
    private Label _regGames, _regWins, _regLosses, _regPct;
    private Label _poGames, _poWins, _poLosses, _poPct;

    // Relationships
    private VisualElement _circleTrust, _circleMorale, _circleFanConfidence;
    private Label _valTrust, _valMorale, _valFanConfidence;

    // Objective
    private Label _managerObjectiveTitle;
    private Label _managerObjectivePosition;
    private Label _managerObjectiveStatus;

    // Rings
    private VisualElement _ringsTrophy;
    private Label _ringsCount;

    // Monthly Awards
    private VisualElement _monthlyAwardsIcon;
    private Label _monthlyAwards;

    // Ranking
    private VisualElement _rankingBody;

    // Config modal
    private VisualElement _configModalOverlay;
    private VisualElement _configModalBox;
    private Button _btnConfigCerrar;
    private CustomSlider _configSliderMaster;
    private CustomSlider _configSliderMusic;
    private CustomSlider _configSliderSFX;
    private Label _configLabelMaster;
    private Label _configLabelMusic;
    private Label _configLabelSFX;
    private Button _configBtnQualityLow;
    private Button _configBtnQualityMedium;
    private Button _configBtnQualityHigh;
    private Button _configBtnQualityUltra;

    // Config confirm modals
    private VisualElement _configMainMenuConfirmOverlay;
    private Button _configBtnMainMenu;
    private Button _configBtnMainMenuYes;
    private Button _configBtnMainMenuNo;
    private VisualElement _configExitConfirmOverlay;
    private Button _configBtnExit;
    private Button _configBtnExitYes;
    private Button _configBtnExitNo;

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
        InitConfigModal();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");

        _regGames  = _root.Q<Label>("RegGames");
        _regWins   = _root.Q<Label>("RegWins");
        _regLosses = _root.Q<Label>("RegLosses");
        _regPct    = _root.Q<Label>("RegPct");

        _poGames  = _root.Q<Label>("PoGames");
        _poWins   = _root.Q<Label>("PoWins");
        _poLosses = _root.Q<Label>("PoLosses");
        _poPct    = _root.Q<Label>("PoPct");

        _circleTrust  = _root.Q<VisualElement>("CircleTrust");
        _circleMorale = _root.Q<VisualElement>("CircleMorale");
        _circleFanConfidence = _root.Q<VisualElement>("CircleFanConfidence");
        _valTrust  = _root.Q<Label>("ValTrust");
        _valMorale = _root.Q<Label>("ValMorale");
        _valFanConfidence = _root.Q<Label>("ValFanConfidence");

        _managerObjectiveTitle = _root.Q<Label>("ManagerObjectiveTitle");
        _managerObjectivePosition = _root.Q<Label>("ManagerObjectivePosition");
        _managerObjectiveStatus = _root.Q<Label>("ManagerObjectiveStatus");

        _ringsTrophy = _root.Q<VisualElement>("ManagerRingsTrophy");
        _ringsCount = _root.Q<Label>("ManagerRingsCount");

        _monthlyAwardsIcon = _root.Q<VisualElement>("ManagerMonthlyAwardsIcon");
        _monthlyAwards = _root.Q<Label>("ManagerMonthlyAwards");

        _rankingBody = _root.Q<VisualElement>("RankingBody");

        // Apply explicit inline styles to all manager panels as a safety net
        var panelBg = new Color(0.078f, 0.094f, 0.133f, 1f); // rgb(20, 24, 34)
        var borderColor = new Color(0.137f, 0.161f, 0.227f, 1f); // rgb(35, 41, 58)
        foreach (var panel in _root.Query<VisualElement>(null, "manager-panel").Build())
        {
            panel.style.backgroundColor = new StyleColor(panelBg);
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopColor = new StyleColor(borderColor);
            panel.style.borderBottomColor = new StyleColor(borderColor);
            panel.style.borderLeftColor = new StyleColor(borderColor);
            panel.style.borderRightColor = new StyleColor(borderColor);
            panel.style.borderTopLeftRadius = 8;
            panel.style.borderTopRightRadius = 8;
            panel.style.borderBottomLeftRadius = 8;
            panel.style.borderBottomRightRadius = 8;
            panel.style.paddingTop = 14;
            panel.style.paddingBottom = 14;
            panel.style.paddingLeft = 16;
            panel.style.paddingRight = 16;
        }
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _standingGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        _standingGames.AddRange(DatabaseManager.Instance.GetPlayoffGames(_manager.id));
        _standingGames.AddRange(DatabaseManager.Instance.GetPlayInGames(_manager.id));
    }

    void RegisterCallbacks()
    {
        SidebarController.Attach(_root, GameScreen.Manager);
        HeaderController.Attach(_root);
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetDefaultCursor();
            RegisterHandCursors();
        }
    }

    void RegisterHandCursors()
    {
        foreach (var btn in _root.Query<Button>(null, "nav-item").Build())
            CursorManager.Instance.RegisterHandCursor(btn);
        foreach (var btn in _root.Query<Button>(null, "nav-submenu-item").Build())
            CursorManager.Instance.RegisterHandCursor(btn);

        var cursorTargets = new[] { "BtnAction", "ConfigIcon",
            "NavDashboard", "NavRoster", "NavCalendar", "NavResults", "NavStandings",
            "NavPalmares", "NavPlayoffs", "NavStats", "NavMarket", "NavFinances",
            "NavArena", "NavManager", "NavMessages" };
        foreach (var name in cursorTargets)
        {
            var el = _root.Q<VisualElement>(name);
            if (el != null)
                CursorManager.Instance.RegisterHandCursor(el);
        }
    }

    void RegisterNavButtons()
    {
        var allSubmenus = new[]
        {
            _root.Q<VisualElement>("RosterSubmenu"),
            _root.Q<VisualElement>("PalmaresSubmenu"),
            _root.Q<VisualElement>("MarketSubmenu"),
            _root.Q<VisualElement>("FinanceSubmenu"),
        };

        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("RosterSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
        });
        _root.Q<Button>("SubmenuJugadores")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        _root.Q<Button>("SubmenuEmpleados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Employees); });
        _root.Q<Button>("SubmenuLesionados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Injured); });
        _root.Q<Button>("SubmenuQuinteto")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Quinteto); });

        _root.Q<Button>("SubmenuEntrenamiento")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Training); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("PalmaresSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
        });
        _root.Q<Button>("SubmenuPalmares")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Palmares); });
        _root.Q<Button>("SubmenuRecords")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Records); });
        _root.Q<Button>("SubmenuPremios")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Premios); });
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });

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
        _root.Q<Button>("SubmenuSponsors")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("SubmenuTV")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavManager")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Manager); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OpenConfigModal(); });
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
            {"NavMarketIcon", "mercado"},
            {"NavFinancesIcon", "finanzas"},
            {"NavArenaIcon", "pabellon"},
            {"NavManagerIcon", "manager"},
            {"NavMessagesIcon", "mensajes"},
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
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Manager] Header error: {ex.Message}"); }
        RefreshTitle();
        RefreshStats();
        RefreshRelationships();
        RefreshObjective();
        RefreshRings();
        RefreshMonthlyAwards();
        RefreshRanking();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos) logoDict[s.name] = s;

        if (logoDict.TryGetValue(_myTeam.logo, out var sprite))
        {
            var logoEl = _root.Q<VisualElement>("HeaderTeamLogo");
            if (logoEl != null)
                logoEl.style.backgroundImage = new StyleBackground(sprite);
        }

        var headerTeamName = _root.Q<Label>("HeaderTeamName");
        if (headerTeamName != null) headerTeamName.text = _myTeam.name.ToUpper();

        var headerManagerName = _root.Q<Label>("HeaderManagerName");
        if (headerManagerName != null) headerManagerName.text = $"Manager: {_manager.name}";

        var budgetLabel = _root.Q<Label>("HeaderBudget");
        if (budgetLabel != null)
        {
            budgetLabel.text = $"${_myTeam.budget / 1_000_000}M";
            budgetLabel.style.color = _myTeam.budget < 0
                ? new StyleColor(new Color32(192, 57, 43, 255))
                : new StyleColor(new Color32(39, 174, 96, 255));
        }

        if (_season != null)
        {
            var headerSeason = _root.Q<Label>("HeaderSeason");
            if (headerSeason != null) headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";

            var headerDate = _root.Q<Label>("HeaderDate");
            if (headerDate != null && _season.current_game_day >= 0 && !string.IsNullOrEmpty(_season.current_date))
            {
                try { headerDate.text = System.DateTime.Parse(_season.current_date).ToString("dd/MM/yyyy"); } catch { }
            }
        }
    }

    void RefreshTitle()
    {
        if (_manager == null) return;
        var title = _root.Q<Label>("ManagerTitle");
        if (title != null) title.text = _manager.name.ToUpper();
    }

    // ── STATS ────────────────────────────────────────────────────

    void RefreshStats()
    {
        if (_manager == null) return;

        // Current season from DB
        int curRegW = 0, curRegL = 0;
        int curPoW = 0, curPoL = 0;

        if (_myTeam != null && _standingGames != null)
        {
            var regularGames = _standingGames
                .Where(g => g.game_type == "regular" && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id) && g.is_played == 1)
                .ToList();
            curRegW = regularGames.Count(g =>
                (g.home_team_id == _myTeam.id && g.home_score > g.away_score) ||
                (g.away_team_id == _myTeam.id && g.away_score > g.home_score));
            curRegL = regularGames.Count - curRegW;

            var playoffGames = _standingGames
                .Where(g => g.is_played == 1 && (g.game_type == "playoff" || g.game_type == "playin") && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id))
                .ToList();
            curPoW = playoffGames.Count(g =>
                (g.home_team_id == _myTeam.id && g.home_score > g.away_score) ||
                (g.away_team_id == _myTeam.id && g.away_score > g.home_score));
            curPoL = playoffGames.Count - curPoW;
        }

        // Career totals = archived + current season
        int regW = _manager.career_reg_wins   + curRegW;
        int regL = _manager.career_reg_losses + curRegL;
        int regT = regW + regL;
        int poW  = _manager.career_po_wins    + curPoW;
        int poL  = _manager.career_po_losses  + curPoL;
        int poT  = poW + poL;

        if (_regGames != null) _regGames.text = regT.ToString();
        if (_regWins != null)  _regWins.text  = regW.ToString();
        if (_regLosses != null) _regLosses.text = regL.ToString();
        if (_regPct != null)   _regPct.text   = regT > 0 ? ((float)regW / regT).ToString("F3") : ".000";

        if (_poGames != null) _poGames.text = poT.ToString();
        if (_poWins != null)  _poWins.text  = poW.ToString();
        if (_poLosses != null) _poLosses.text = poL.ToString();
        if (_poPct != null)   _poPct.text   = poT > 0 ? ((float)poW / poT).ToString("F3") : ".000";
    }

    // ── RELATIONSHIPS ────────────────────────────────────────────

    void RefreshRelationships()
    {
        if (_manager == null) return;
        SetCircle(_circleTrust, _valTrust, _manager.trust);
        SetCircle(_circleMorale, _valMorale, _manager.morale);
        SetCircle(_circleFanConfidence, _valFanConfidence, _manager.fan_confidence);
    }

    void SetCircle(VisualElement circle, Label val, int value)
    {
        if (circle == null || val == null) return;

        Color bgColor, borderColor;
        if (value >= 70)
        {
            bgColor = new Color32(39, 174, 96, 40);
            borderColor = new Color32(39, 174, 96, 255);
        }
        else if (value >= 40)
        {
            bgColor = new Color32(212, 160, 23, 40);
            borderColor = new Color32(212, 160, 23, 255);
        }
        else
        {
            bgColor = new Color32(192, 57, 43, 40);
            borderColor = new Color32(192, 57, 43, 255);
        }

        circle.style.backgroundColor = new StyleColor(bgColor);
        circle.style.borderTopColor = new StyleColor(borderColor);
        circle.style.borderBottomColor = new StyleColor(borderColor);
        circle.style.borderLeftColor = new StyleColor(borderColor);
        circle.style.borderRightColor = new StyleColor(borderColor);

        val.text = $"{value}%";
    }

    // ── OBJECTIVE ────────────────────────────────────────────────

    int GetMyTeamConferenceRank()
    {
        if (_myTeam == null || _allTeams == null || _standingGames == null) return 0;

        var confTeams = _allTeams.Where(t => t.conference == _myTeam.conference).ToList();
        var standings = new List<(TeamData team, int wins, int losses)>();
        foreach (var t in confTeams)
        {
            var tg = _standingGames
                .Where(g => g.is_played == 1 && g.game_type == "regular" && (g.home_team_id == t.id || g.away_team_id == t.id))
                .ToList();
            int w = tg.Count(g =>
                (g.home_team_id == t.id && g.home_score > g.away_score) ||
                (g.away_team_id == t.id && g.away_score > g.home_score));
            standings.Add((t, w, tg.Count - w));
        }
        standings.Sort((a, b) =>
        {
            float pctA = a.wins + a.losses > 0 ? (float)a.wins / (a.wins + a.losses) : 0;
            float pctB = b.wins + b.losses > 0 ? (float)b.wins / (b.wins + b.losses) : 0;
            if (pctB != pctA) return pctB.CompareTo(pctA);
            if (a.losses != b.losses) return a.losses.CompareTo(b.losses);
            return b.wins.CompareTo(a.wins);
        });
        for (int i = 0; i < standings.Count; i++)
            if (standings[i].team.id == _myTeam.id) return i + 1;
        return 0;
    }

    void RefreshObjective()
    {
        if (_myTeam == null) return;

        string obj = _myTeam.objective ?? "--";
        if (_managerObjectiveTitle != null)
            _managerObjectiveTitle.text = $"OBJETIVO DE TEMPORADA: {obj.ToUpper()}";

        int rank = GetMyTeamConferenceRank();
        bool met = false;
        if (rank > 0)
        {
            if (obj == "Zona tranquila") met = rank <= 12;
            else if (obj == "Play-In") met = rank <= 10;
            else if (obj == "Playoffs") met = rank <= 6;
            else if (obj == "Campeonato") met = rank <= 2;
        }

        if (_managerObjectivePosition != null)
        {
            string conf = _myTeam.conference == "East" ? "Este" : "Oeste";
            _managerObjectivePosition.text = rank > 0
                ? $"Puesto {rank}º en la conferencia {conf}"
                : $"Conferencia {conf}";
        }

        if (_managerObjectiveStatus != null)
        {
            if (rank <= 0)
            {
                _managerObjectiveStatus.text = "";
                _managerObjectiveStatus.RemoveFromClassList("manager-objective-status--met");
                _managerObjectiveStatus.RemoveFromClassList("manager-objective-status--not-met");
            }
            else if (met)
            {
                _managerObjectiveStatus.text = "OBJETIVO CUMPLIDO";
                _managerObjectiveStatus.RemoveFromClassList("manager-objective-status--not-met");
                _managerObjectiveStatus.AddToClassList("manager-objective-status--met");
            }
            else
            {
                _managerObjectiveStatus.text = "OBJETIVO NO CUMPLIDO";
                _managerObjectiveStatus.RemoveFromClassList("manager-objective-status--met");
                _managerObjectiveStatus.AddToClassList("manager-objective-status--not-met");
            }
        }
    }

    // ── RINGS ────────────────────────────────────────────────────

    void RefreshRings()
    {
        if (_manager == null) return;

        var tex = Resources.Load<Texture2D>("Icons/trofeo64px");
        if (tex != null && _ringsTrophy != null)
            _ringsTrophy.style.backgroundImage = new StyleBackground(tex);

        if (_ringsCount == null) return;

        int rings = _manager.championships;

        // If current season just ended (FinalsRecord exists but not yet archived), count it too
        if (_myTeam != null && _season != null)
        {
            string seasonLabel = $"{_season.year_start}-{_season.year_end.ToString().Substring(2)}";
            var finals = DatabaseManager.Instance.GetFinalsRecords()
                .FirstOrDefault(f => f.season == seasonLabel);
            if (finals != null && finals.champ_name == _myTeam.name)
            {
                // Check if this championship is already counted in archived stats.
                // It's already counted if seasons_completed includes this season,
                // which means StartNewSeason already archived it.
                // We can detect this by comparing: if the FinalsRecord's season label
                // matches the LAST archived season, it's already counted.
                // Simple heuristic: if seasons_completed > 0 and the archived
                // championships count includes this one, we'd double-count.
                // The safest check: was this season already archived?
                // seasons_completed was incremented in StartNewSeason. So if
                // the season ended AND StartNewSeason ran, seasons_completed includes it.
                // We can't easily detect this from here, so use a simpler approach:
                // only add 1 if the current season's games still exist in DB
                // (meaning StartNewSeason hasn't run yet)
                if (_standingGames != null && _standingGames.Any(g => g.is_played == 1))
                    rings += 1;
            }
        }

        _ringsCount.text = rings.ToString();
    }

    // ── MONTHLY AWARDS ──────────────────────────────────────────

    void RefreshMonthlyAwards()
    {
        if (_manager == null || _monthlyAwards == null) return;

        var tex = Resources.Load<Texture2D>("Icons/manager_mes");
        if (tex != null && _monthlyAwardsIcon != null)
            _monthlyAwardsIcon.style.backgroundImage = new StyleBackground(tex);

        int count = DatabaseManager.Instance.CountManagerOfTheMonthWins(_manager.id);
        _monthlyAwards.text = count.ToString();
    }

    // ── RANKING ──────────────────────────────────────────────────

    void RefreshRanking()
    {
        _rankingBody?.Clear();

        var ranking = DatabaseManager.Instance.GetCoachRanking();
        if (ranking == null || ranking.Count == 0) return;

        int rank = 0;
        var evenBg = new Color(0.059f, 0.071f, 0.102f, 1f); // rgb(15, 18, 26)
        var oddBg = new Color(0f, 0f, 0f, 0f); // transparent
        foreach (var coach in ranking)
        {
            rank++;
            var row = new VisualElement();
            row.AddToClassList("ranking-row");

            if (coach.status == "player")
                row.AddToClassList("ranking-row--player");
            else if (rank % 2 == 0)
                row.style.backgroundColor = new StyleColor(evenBg);

            var rankLabel = new Label(rank.ToString());
            rankLabel.AddToClassList("ranking-col-rank");
            row.Add(rankLabel);

            var nameLabel = new Label(coach.name);
            nameLabel.AddToClassList("ranking-col-name");
            row.Add(nameLabel);

            var teamAbbrev = "—";
            if (coach.status == "active" || coach.status == "player")
            {
                var team = _allTeams?.FirstOrDefault(t => t.id == coach.team_id);
                if (team != null) teamAbbrev = team.abbreviation;
            }
            var teamLabel = new Label(teamAbbrev);
            teamLabel.AddToClassList("ranking-col-team");
            row.Add(teamLabel);

            var scoreLabel = new Label(coach.score.ToString());
            scoreLabel.AddToClassList("ranking-col-score");
            row.Add(scoreLabel);

            var badgeContainer = new VisualElement();
            badgeContainer.AddToClassList("ranking-col-badge");

            if (coach.status == "historical")
            {
                var badge = new Label("HISTÓRICO");
                badge.AddToClassList("ranking-badge");
                badge.AddToClassList("ranking-badge--historical");
                badgeContainer.Add(badge);
            }
            else if (coach.status == "inactive")
            {
                var badge = new Label("INACTIVO");
                badge.AddToClassList("ranking-badge");
                badge.AddToClassList("ranking-badge--inactive");
                badgeContainer.Add(badge);
            }
            else if (coach.status == "player")
            {
                var badge = new Label("TÚ");
                badge.AddToClassList("ranking-badge");
                badge.AddToClassList("ranking-badge--player");
                badgeContainer.Add(badge);
            }
            else if (coach.status == "active")
            {
                var badge = new Label("ACTIVO");
                badge.AddToClassList("ranking-badge");
                badge.AddToClassList("ranking-badge--active");
                badgeContainer.Add(badge);
            }

            row.Add(badgeContainer);
            _rankingBody.Add(row);
        }
    }

    // ── CONFIG MODAL ─────────────────────────────────────────────

    void InitConfigModal()
    {
        _configModalOverlay = _root.Q<VisualElement>("ConfigModalOverlay");
        _configModalBox     = _root.Q<VisualElement>("ConfigModalBox");
        _btnConfigCerrar    = _root.Q<Button>("ConfigBtnCerrar");

        _configSliderMaster = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderMaster"),
            _root.Q<VisualElement>("ConfigFillMaster"),
            _root.Q<VisualElement>("ConfigDraggerMaster"));
        _configSliderMusic  = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderMusic"),
            _root.Q<VisualElement>("ConfigFillMusic"),
            _root.Q<VisualElement>("ConfigDraggerMusic"));
        _configSliderSFX    = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderSFX"),
            _root.Q<VisualElement>("ConfigFillSFX"),
            _root.Q<VisualElement>("ConfigDraggerSFX"));
        _configLabelMaster  = _root.Q<Label>("ConfigLabelMaster");
        _configLabelMusic   = _root.Q<Label>("ConfigLabelMusic");
        _configLabelSFX     = _root.Q<Label>("ConfigLabelSFX");
        _configBtnQualityLow    = _root.Q<Button>("ConfigBtnQualityLow");
        _configBtnQualityMedium = _root.Q<Button>("ConfigBtnQualityMedium");
        _configBtnQualityHigh   = _root.Q<Button>("ConfigBtnQualityHigh");
        _configBtnQualityUltra  = _root.Q<Button>("ConfigBtnQualityUltra");

        _configBtnMainMenu     = _root.Q<Button>("ConfigBtnMainMenu");
        _configBtnExit         = _root.Q<Button>("ConfigBtnExit");

        _configMainMenuConfirmOverlay = _root.Q<VisualElement>("ConfigMainMenuConfirmOverlay");
        _configBtnMainMenuYes = _root.Q<Button>("ConfigBtnMainMenuYes");
        _configBtnMainMenuNo  = _root.Q<Button>("ConfigBtnMainMenuNo");

        _configExitConfirmOverlay = _root.Q<VisualElement>("ConfigExitConfirmOverlay");
        _configBtnExitYes = _root.Q<Button>("ConfigBtnExitYes");
        _configBtnExitNo  = _root.Q<Button>("ConfigBtnExitNo");

        _configSliderMaster.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMasterVolume(v);
            UpdateConfigLabels();
        };
        _configSliderMusic.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMusicVolume(v);
            UpdateConfigLabels();
        };
        _configSliderSFX.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetSFXVolume(v);
            UpdateConfigLabels();
        };

        _configBtnQualityLow?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(0); });
        _configBtnQualityMedium?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(1); });
        _configBtnQualityHigh?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(2); });
        _configBtnQualityUltra?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(3); });

        _btnConfigCerrar?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseConfigModal(); });
        _configModalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configModalOverlay)
                { PlayClick(); CloseConfigModal(); }
        });

        _configBtnMainMenu?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenMainMenuConfirmModal(); });
        _configBtnExit?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenExitConfirmModal(); });

        _configBtnMainMenuYes?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        _configBtnMainMenuNo?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseMainMenuConfirmModal();
        });
        _configMainMenuConfirmOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configMainMenuConfirmOverlay)
                { PlayClick(); CloseMainMenuConfirmModal(); }
        });

        _configBtnExitYes?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            QuitGame();
        });
        _configBtnExitNo?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseExitConfirmModal();
        });
        _configExitConfirmOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configExitConfirmOverlay)
                { PlayClick(); CloseExitConfirmModal(); }
        });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnConfigCerrar);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityLow);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityMedium);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityHigh);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityUltra);
            CursorManager.Instance.RegisterHandCursor(_configBtnMainMenu);
            CursorManager.Instance.RegisterHandCursor(_configBtnExit);
            CursorManager.Instance.RegisterHandCursor(_configBtnMainMenuYes);
            CursorManager.Instance.RegisterHandCursor(_configBtnMainMenuNo);
            CursorManager.Instance.RegisterHandCursor(_configBtnExitYes);
            CursorManager.Instance.RegisterHandCursor(_configBtnExitNo);

            if (_configSliderMaster?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderMaster.Container);
            if (_configSliderMusic?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderMusic.Container);
            if (_configSliderSFX?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderSFX.Container);
        }
    }

    void OpenConfigModal()
    {
        CursorManager.Instance?.SetDefaultCursor();
        var am = AudioManager.Instance;
        if (am != null)
        {
            _configSliderMaster.SetValueWithoutNotify(am.MasterVolume);
            _configSliderMusic.SetValueWithoutNotify(am.MusicVolume);
            _configSliderSFX.SetValueWithoutNotify(am.SFXVolume);
            UpdateConfigLabels();
        }
        int q = QualitySettings.GetQualityLevel();
        UpdateConfigQualityButtons(Mathf.Clamp(q, 0, 3));

        _configModalOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configModalOverlay.AddToClassList("modal-overlay--visible");
        _configModalBox.AddToClassList("modal-box--visible");
    }

    void CloseConfigModal()
    {
        _configModalOverlay.RemoveFromClassList("modal-overlay--visible");
        _configModalBox.RemoveFromClassList("modal-box--visible");
    }

    void UpdateConfigLabels()
    {
        var am = AudioManager.Instance;
        if (am == null) return;
        if (_configLabelMaster != null)
            _configLabelMaster.text = $"{Mathf.RoundToInt(am.MasterVolume * 100)}%";
        if (_configLabelMusic != null)
            _configLabelMusic.text  = $"{Mathf.RoundToInt(am.MusicVolume  * 100)}%";
        if (_configLabelSFX != null)
            _configLabelSFX.text    = $"{Mathf.RoundToInt(am.SFXVolume    * 100)}%";
    }

    void SelectConfigQuality(int index)
    {
        AudioManager.Instance?.SetQualityLevel(index);
        UpdateConfigQualityButtons(index);
    }

    void UpdateConfigQualityButtons(int activeIndex)
    {
        var buttons = new[] { _configBtnQualityLow, _configBtnQualityMedium, _configBtnQualityHigh, _configBtnQualityUltra };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].EnableInClassList("settings-quality-btn--active", i == activeIndex);
        }
    }

    void OpenMainMenuConfirmModal()
    {
        CloseConfigModal();
        PlayClick();
        _configMainMenuConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configMainMenuConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseMainMenuConfirmModal()
    {
        _configMainMenuConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.RemoveFromClassList("modal-box--visible");
        OpenConfigModal();
    }

    void OpenExitConfirmModal()
    {
        CloseConfigModal();
        PlayClick();
        _configExitConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configExitConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseExitConfirmModal()
    {
        _configExitConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.RemoveFromClassList("modal-box--visible");
        OpenConfigModal();
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
