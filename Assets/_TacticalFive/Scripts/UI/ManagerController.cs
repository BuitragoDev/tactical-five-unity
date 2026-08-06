using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
    public class ManagerController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Manager;
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

    // Achievements
    private Label _achievementsCount;

    // Ranking
    private VisualElement _rankingBody;
    protected override void CacheReferences()
    {

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

        _achievementsCount = _root.Q<Label>("ManagerAchievementsCount");

        _root.Q<Button>("ManagerBtnLogros")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.Logros);
        });

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
    protected override void LoadData()
    {
        base.LoadData();

        
        

        
        
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _standingGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        _standingGames.AddRange(DatabaseManager.Instance.GetPlayoffGames(_manager.id));
        _standingGames.AddRange(DatabaseManager.Instance.GetPlayInGames(_manager.id));
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Manager] Header error: {ex.Message}"); }
        RefreshTitle();
        RefreshStats();
        RefreshRelationships();
        RefreshObjective();
        RefreshRings();
        RefreshMonthlyAwards();
        RefreshAchievements();
        RefreshRanking();
    }
    protected override void RefreshHeader()
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
        if (_regPct != null)   _regPct.text   = regT > 0 ? ((float)regW / regT).ToString("F3", CultureInfo.InvariantCulture) : ".000";

        if (_poGames != null) _poGames.text = poT.ToString();
        if (_poWins != null)  _poWins.text  = poW.ToString();
        if (_poLosses != null) _poLosses.text = poL.ToString();
        if (_poPct != null)   _poPct.text   = poT > 0 ? ((float)poW / poT).ToString("F3", CultureInfo.InvariantCulture) : ".000";
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
        if (_myTeam == null) return 0;
        return ObjectiveHelper.GetConferenceRank(_myTeam.id, _myTeam.conference, _allTeams, _standingGames);
    }

    void RefreshObjective()
    {
        if (_myTeam == null) return;

        string obj = _myTeam.objective ?? "--";
        if (_managerObjectiveTitle != null)
            _managerObjectiveTitle.text = $"OBJETIVO DE TEMPORADA: {obj.ToUpper()}";

        int rank = GetMyTeamConferenceRank();
        bool met = ObjectiveHelper.IsObjectiveMet(_myTeam.objective, rank);

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

    // ── ACHIEVEMENTS ─────────────────────────────────────────────

    void RefreshAchievements()
    {
        if (_manager == null || _achievementsCount == null) return;

        var tex = Resources.Load<Texture2D>("Icons/trofeo64px");
        if (tex != null)
        {
            var icon = _root.Q<VisualElement>("ManagerAchievementsIcon");
            if (icon != null) icon.style.backgroundImage = new StyleBackground(tex);
        }

        int count = DatabaseManager.Instance.CountAchievements(_manager.id);
        int total = AchievementCatalog.All.Length;
        _achievementsCount.text = $"{count} / {total}";
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
}
