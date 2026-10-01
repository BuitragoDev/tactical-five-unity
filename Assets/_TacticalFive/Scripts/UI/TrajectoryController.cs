using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class TrajectoryController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Trajectory;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerChemistry;
    private Label _headerSeason;
    private Label _headerDate;

    // Cabecera jugador (banner)
    private VisualElement _trajectoryPhoto;
    private Label _trajectoryPlayerName;
    private Label _trajectoryNumber;
    private Label _trajectoryPlayerPos;
    private VisualElement _trajectoryTeamLogo;
    private Label _trajectoryPlayerTeam;
    private VisualElement _trajectoryBannerBg;
    private Label _trajectoryOvr;
    private VisualElement _trajectoryOvrBlock;
    private Button _btnGoToProfile;
    private VisualElement _trajectoryRingsIcon;
    private Label _trajectoryRingsCount;
    private VisualElement _trajectoryFinalsIcon;
    private Label _trajectoryFinalsCount;
    private VisualElement _trajectoryMvpFinalsIcon;
    private Label _trajectoryMvpFinalsCount;
    private VisualElement _trajectoryMvpSeasonIcon;
    private Label _trajectoryMvpSeasonCount;
    private Dictionary<string, Sprite> _teamLogos = new();

    // Logros
    private VisualElement _trajectoryAwards;
    private VisualElement _trajectoryAwardsBody;

    // Hitos de carrera (E1)
    private VisualElement _trajectoryMilestones;
    private VisualElement _trajectoryMilestonesBody;

    // Tabla
    private VisualElement _trajectoryTableBody;
    private VisualElement _trajectoryTableTotals;
    private VisualElement _trajectoryTableScrollViewport;
    private VisualElement _trajectoryTableScrollTrack;
    private VisualElement _trajectoryTableScrollThumb;
    private CustomScrollbar.Handle _tableScrollBar;
    private CustomScrollbar.Handle _milestonesScrollBar;
    private CustomScrollbar.Handle _awardsScrollBar;
    private VisualElement _trajectoryEmpty;
    private VisualElement _trajectoryStatsSection;
    private PlayerData _player;
    private HashSet<int> _scoutedPlayerIds;
    private List<PlayerCareerSeasonRow> _careerHistory;
    private List<PlayerAwardEntry> _awards;
    protected override void CacheReferences()
    {
        _trajectoryPhoto = _root.Q<VisualElement>("TrajectoryPhoto");
        _trajectoryPlayerName = _root.Q<Label>("TrajectoryPlayerName");
        _trajectoryNumber = _root.Q<Label>("TrajectoryNumber");
        _trajectoryPlayerPos = _root.Q<Label>("TrajectoryPlayerPos");
        _trajectoryTeamLogo = _root.Q<VisualElement>("TrajectoryTeamLogo");
        _trajectoryPlayerTeam = _root.Q<Label>("TrajectoryPlayerTeam");
        _trajectoryBannerBg = _root.Q<VisualElement>("TrajectoryBannerBg");
        _btnGoToProfile = _root.Q<Button>("BtnGoToProfile");
        _trajectoryOvr = _root.Q<Label>("TrajectoryOvr");
        _trajectoryOvrBlock = _root.Q<VisualElement>("TrajectoryOvrBlock");
        _trajectoryRingsIcon = _root.Q<VisualElement>("TrajectoryRingsIcon");
        _trajectoryRingsCount = _root.Q<Label>("TrajectoryRingsCount");
        _trajectoryFinalsIcon = _root.Q<VisualElement>("TrajectoryFinalsIcon");
        _trajectoryFinalsCount = _root.Q<Label>("TrajectoryFinalsCount");
        _trajectoryMvpFinalsIcon = _root.Q<VisualElement>("TrajectoryMvpFinalsIcon");
        _trajectoryMvpFinalsCount = _root.Q<Label>("TrajectoryMvpFinalsCount");
        _trajectoryMvpSeasonIcon = _root.Q<VisualElement>("TrajectoryMvpSeasonIcon");
        _trajectoryMvpSeasonCount = _root.Q<Label>("TrajectoryMvpSeasonCount");
        _trajectoryAwards = _root.Q<VisualElement>("TrajectoryAwards");
        _trajectoryAwardsBody = _root.Q<VisualElement>("TrajectoryAwardsBody");
        _trajectoryMilestones = _root.Q<VisualElement>("TrajectoryMilestones");
        _trajectoryMilestonesBody = _root.Q<VisualElement>("TrajectoryMilestonesBody");
        _trajectoryTableBody = _root.Q<VisualElement>("TrajectoryTableBody");
        _trajectoryTableTotals = _root.Q<VisualElement>("TrajectoryTableTotals");
        _trajectoryTableScrollViewport = _root.Q<VisualElement>("TrajectoryTableScrollViewport");
        _trajectoryTableScrollTrack = _root.Q<VisualElement>("TrajectoryTableScrollTrack");
        _trajectoryTableScrollThumb = _root.Q<VisualElement>("TrajectoryTableScrollThumb");
        _trajectoryEmpty = _root.Q<VisualElement>("TrajectoryEmpty");
        _trajectoryStatsSection = _root.Q<VisualElement>("TrajectoryStatsSection");

        // Barra de scroll propia de la tabla de temporadas: se re-enlaza en cada visita
        // (Unity recrea el árbol del UIDocument al reactivar la pantalla).
        _tableScrollBar = CustomScrollbar.Bind(_trajectoryTableScrollViewport, _trajectoryTableBody,
                                               _trajectoryTableScrollTrack, _trajectoryTableScrollThumb);

        // Barras propias de los paneles de hitos y logros (2 columnas).
        _milestonesScrollBar = CustomScrollbar.Bind(
            _root.Q<VisualElement>("TrajectoryMilestonesViewport"), _trajectoryMilestonesBody,
            _root.Q<VisualElement>("TrajectoryMilestonesScrollTrack"), _root.Q<VisualElement>("TrajectoryMilestonesScrollThumb"));
        _awardsScrollBar = CustomScrollbar.Bind(
            _root.Q<VisualElement>("TrajectoryAwardsViewport"), _trajectoryAwardsBody,
            _root.Q<VisualElement>("TrajectoryAwardsScrollTrack"), _root.Q<VisualElement>("TrajectoryAwardsScrollThumb"));

        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerChemistry = _root.Q<Label>("HeaderChemistry");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
    }
    protected override void LoadData()
    {
        base.LoadData();

        
        
        

        int playerId = ScreenManager.SelectedPlayerId;
        _player = DatabaseManager.Instance.GetPlayerById(playerId);
        if (_player == null) return;

        _careerHistory = DatabaseManager.Instance.GetPlayerCareerHistory(playerId, _manager.id);
        _awards = DatabaseManager.Instance.GetPlayerAwards(playerId);

        _scoutedPlayerIds = DatabaseManager.Instance.GetScoutedPlayerIds(_myTeam != null ? _myTeam.id : 0);

        _teamLogos.Clear();
        foreach (var s in TeamImages.Logos(64))
            _teamLogos[s.name] = s;
    }
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _btnGoToProfile?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_player == null) return;
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.PlayerProfile);
        });
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Trajectory] RefreshHeader error: {ex.Message}"); }
        if (_player == null) return;
        FillPlayerHeader();
        BuildAwards();
        BuildMilestones();
        BuildStatsTable();
    }

    void FillPlayerHeader()
    {
        _trajectoryPlayerName.text = $"{_player.first_name} {_player.last_name}".ToUpper();
        _trajectoryPlayerPos.text = $"{PositionCodes.GetName(_player.position)} · {PositionCodes.GetName(_player.secondary_position)}";

        // Dorsal (oculto si no tiene).
        _trajectoryNumber.text = _player.number > 0 ? $"#{_player.number}" : "";
        _trajectoryNumber.style.display = _player.number > 0 ? DisplayStyle.Flex : DisplayStyle.None;

        // Equipo del jugador (o agente libre) + escudo.
        var team = DatabaseManager.Instance.GetTeamById(_player.team_id);
        _trajectoryPlayerTeam.text = team?.name ?? "Agente libre";
        if (team != null && _teamLogos.TryGetValue(team.logo, out var logoSprite))
            _trajectoryTeamLogo.style.backgroundImage = new StyleBackground(logoSprite);
        else
            _trajectoryTeamLogo.style.backgroundImage = StyleKeyword.None;

        // Imagen del pabellón del equipo como fondo del banner.
        if (_trajectoryBannerBg != null)
        {
            string arenaKey = team?.logo;
            if (arenaKey == "sixers") arenaKey = "76ers";
            var arenaSprite = !string.IsNullOrEmpty(arenaKey) ? TeamImages.Arena(arenaKey) : null;
            if (arenaSprite == null) arenaSprite = TeamImages.Arena("default");
            _trajectoryBannerBg.style.backgroundImage = arenaSprite != null ? new StyleBackground(arenaSprite) : StyleKeyword.None;
        }

        int ovr = _player.GetCalculatedAverage();
        // Fog-of-war: si no es de mi equipo y no está ojeado, se muestra el rango, no la media.
        bool canView = FogOfWarHelper.CanViewRatings(_player, _myTeam.id, _scoutedPlayerIds);
        _trajectoryOvr.text = canView ? ovr.ToString() : FogOfWarHelper.GetRatingBand(ovr, _player.id);

        // MED por rango (como los atributos): <40 rojo, <70 dorado, resto verde.
        // Con fog-of-war el badge queda neutro.
        if (_trajectoryOvrBlock != null)
        {
            _trajectoryOvrBlock.RemoveFromClassList("trajectory-ovr-block--high");
            _trajectoryOvrBlock.RemoveFromClassList("trajectory-ovr-block--mid");
            _trajectoryOvrBlock.RemoveFromClassList("trajectory-ovr-block--low");
            if (canView)
            {
                if (ovr < 40) _trajectoryOvrBlock.AddToClassList("trajectory-ovr-block--low");
                else if (ovr < 70) _trajectoryOvrBlock.AddToClassList("trajectory-ovr-block--mid");
                else _trajectoryOvrBlock.AddToClassList("trajectory-ovr-block--high");
            }
        }

        var ringsSprite = Resources.Load<Sprite>("Icons/trofeo64px");
        if (ringsSprite != null)
            _trajectoryRingsIcon.style.backgroundImage = new StyleBackground(ringsSprite);
        _trajectoryRingsCount.text = _player.rings.ToString();

        var finalsSprite = Resources.Load<Sprite>("Icons/vs_icon");
        if (finalsSprite != null)
            _trajectoryFinalsIcon.style.backgroundImage = new StyleBackground(finalsSprite);
        _trajectoryFinalsCount.text = _player.finals_played.ToString();

        var mvpFinalsSprite = Resources.Load<Sprite>("Icons/mvp_finals");
        if (mvpFinalsSprite != null)
            _trajectoryMvpFinalsIcon.style.backgroundImage = new StyleBackground(mvpFinalsSprite);
        _trajectoryMvpFinalsCount.text = _player.finals_mvps.ToString();

        var mvpSeasonSprite = Resources.Load<Sprite>("Icons/trofeo3");
        if (mvpSeasonSprite != null)
            _trajectoryMvpSeasonIcon.style.backgroundImage = new StyleBackground(mvpSeasonSprite);
        _trajectoryMvpSeasonCount.text = _player.season_mvps.ToString();

        var tex = PlayerPhotoHelper.Load(_player.id, _player.photo);
        _trajectoryPhoto.style.backgroundImage = tex != null ? new StyleBackground(tex) : StyleKeyword.None;
    }

    void BuildAwards()
    {
        if (_awards == null || _awards.Count == 0)
        {
            _trajectoryAwards.style.display = DisplayStyle.None;
            return;
        }
        _trajectoryAwards.style.display = DisplayStyle.Flex;
        _trajectoryAwardsBody.Clear();

        foreach (var award in _awards)
        {
            var badge = new Label();
            badge.AddToClassList("trajectory-award-badge");
            badge.AddToClassList(GetAwardClass(award.award_type));
            badge.text = GetAwardText(award);
            _trajectoryAwardsBody.Add(badge);
        }

        CustomScrollbar.Reset(_awardsScrollBar);
    }

    /// <summary>Hitos de carrera del jugador (E1): conseguidos + el siguiente con progreso.</summary>
    void BuildMilestones()
    {
        if (_trajectoryMilestones == null || _trajectoryMilestonesBody == null || _player == null) return;
        _trajectoryMilestonesBody.Clear();

        var registered = DatabaseManager.Instance.GetPlayerMilestones(_player.id);
        var careers = DatabaseManager.Instance.BuildCareerTotals(_manager.id, _season?.id ?? 0);
        careers.TryGetValue(_player.id, out var career);
        var next = MilestoneHelper.NextOverall(career);

        if (registered.Count == 0 && next == null)
        {
            _trajectoryMilestones.style.display = DisplayStyle.None;
            return;
        }
        _trajectoryMilestones.style.display = DisplayStyle.Flex;

        var seasonLabels = DatabaseManager.Instance.GetAllSeasons()
            .ToDictionary(x => x.id, x => $"{x.year_start}-{x.year_end % 100:D2}");

        foreach (var m in registered)
        {
            var def = MilestoneCatalog.ByKey(m.milestone);
            if (def == null) continue;
            string date = seasonLabels.TryGetValue(m.season_id, out var lbl) && !string.IsNullOrEmpty(lbl)
                ? $"T {lbl}" : "";
            _trajectoryMilestonesBody.Add(MilestoneUI.Achieved(def, date, "trajectory"));
        }

        if (next != null)
        {
            var n = next.Value;
            _trajectoryMilestonesBody.Add(MilestoneUI.Next(n.next, n.current, n.percent, "trajectory", withBar: true));
        }

        CustomScrollbar.Reset(_milestonesScrollBar);
    }

    void BuildStatsTable()
    {
        if (_careerHistory == null || _careerHistory.Count == 0)
        {
            if (_trajectoryStatsSection != null)
                _trajectoryStatsSection.style.display = DisplayStyle.None;
            _trajectoryEmpty.style.display = DisplayStyle.Flex;
            return;
        }

        if (_trajectoryStatsSection != null)
            _trajectoryStatsSection.style.display = DisplayStyle.Flex;
        _trajectoryEmpty.style.display = DisplayStyle.None;
        _trajectoryTableBody.Clear();

        var fmt = System.Globalization.CultureInfo.InvariantCulture;

        // Merge rows with same season_id (mid-season trades produce multiple rows)
        var merged = _careerHistory
            .GroupBy(s => s.season_id)
            .Select(g =>
            {
                var rows = g.ToList();
                if (rows.Count == 1) return rows[0];
                return new PlayerCareerSeasonRow
                {
                    season_id = rows[0].season_id,
                    year_start = rows[0].year_start,
                    year_end = rows[0].year_end,
                    team_id = rows[0].team_id,
                    team_abbreviation = string.Join(" / ", rows.Select(r => r.team_abbreviation ?? "—")),
                    team_name = string.Join(" / ", rows.Select(r => r.team_name ?? "—")),
                    games = rows.Sum(r => r.games),
                    total_minutes = rows.Sum(r => r.total_minutes),
                    total_points = rows.Sum(r => r.total_points),
                    total_rebounds = rows.Sum(r => r.total_rebounds),
                    total_assists = rows.Sum(r => r.total_assists),
                    total_steals = rows.Sum(r => r.total_steals),
                    total_blocks = rows.Sum(r => r.total_blocks),
                    total_rating = rows.Sum(r => r.total_rating)
                };
            })
            .ToList();

        foreach (var season in merged)
        {
            var row = new VisualElement();
            row.AddToClassList("trajectory-table-row");

            row.Add(CreateCell("td-season", $"{season.year_start}-{season.year_end}"));
            row.Add(CreateCell("td-team", season.team_name ?? "—"));
            row.Add(CreateCell("td-gp", season.games.ToString()));
            row.Add(CreateCell("td-mp", season.games > 0
                ? (season.total_minutes / season.games).ToString("F1", fmt) : "0.0"));
            row.Add(CreateCell("td-pts", season.games > 0
                ? ((float)season.total_points / season.games).ToString("F1", fmt) : "0.0"));
            row.Add(CreateCell("td-reb", season.games > 0
                ? ((float)season.total_rebounds / season.games).ToString("F1", fmt) : "0.0"));
            row.Add(CreateCell("td-ast", season.games > 0
                ? ((float)season.total_assists / season.games).ToString("F1", fmt) : "0.0"));
            row.Add(CreateCell("td-stl", season.games > 0
                ? ((float)season.total_steals / season.games).ToString("F1", fmt) : "0.0"));
            row.Add(CreateCell("td-blk", season.games > 0
                ? ((float)season.total_blocks / season.games).ToString("F1", fmt) : "0.0"));
            row.Add(CreateCell("td-val", season.games > 0
                ? ((float)season.total_rating / season.games).ToString("F1", fmt) : "0.0"));

            _trajectoryTableBody.Add(row);
        }

        // Fila de totales carrera (calculados desde merged)
        int totalGames = merged.Sum(s => s.games);
        double totalMinutes = merged.Sum(s => s.total_minutes);
        int totalPts = merged.Sum(s => s.total_points);
        int totalReb = merged.Sum(s => s.total_rebounds);
        int totalAst = merged.Sum(s => s.total_assists);
        int totalStl = merged.Sum(s => s.total_steals);
        int totalBlk = merged.Sum(s => s.total_blocks);
        int totalRat = merged.Sum(s => s.total_rating);

        // Fila de totales carrera: va en el footer FIJO (fuera del scroll).
        if (_trajectoryTableTotals != null)
        {
            _trajectoryTableTotals.Clear();
            _trajectoryTableTotals.Add(CreateCell("td-season", "TOTAL"));
            _trajectoryTableTotals.Add(CreateCell("td-team", ""));
            _trajectoryTableTotals.Add(CreateCell("td-gp", totalGames.ToString()));
            _trajectoryTableTotals.Add(CreateCell("td-mp", totalGames > 0
                ? (totalMinutes / totalGames).ToString("F1", fmt) : "0.0"));
            _trajectoryTableTotals.Add(CreateCell("td-pts", totalGames > 0
                ? ((float)totalPts / totalGames).ToString("F1", fmt) : "0.0"));
            _trajectoryTableTotals.Add(CreateCell("td-reb", totalGames > 0
                ? ((float)totalReb / totalGames).ToString("F1", fmt) : "0.0"));
            _trajectoryTableTotals.Add(CreateCell("td-ast", totalGames > 0
                ? ((float)totalAst / totalGames).ToString("F1", fmt) : "0.0"));
            _trajectoryTableTotals.Add(CreateCell("td-stl", totalGames > 0
                ? ((float)totalStl / totalGames).ToString("F1", fmt) : "0.0"));
            _trajectoryTableTotals.Add(CreateCell("td-blk", totalGames > 0
                ? ((float)totalBlk / totalGames).ToString("F1", fmt) : "0.0"));
            _trajectoryTableTotals.Add(CreateCell("td-val", totalGames > 0
                ? ((float)totalRat / totalGames).ToString("F1", fmt) : "0.0"));
        }

        // Vuelve al inicio y recalcula el thumb con la lista reconstruida.
        CustomScrollbar.Reset(_tableScrollBar);
    }

    static Label CreateCell(string className, string text)
    {
        var lbl = new Label();
        lbl.AddToClassList(className);
        lbl.text = text;
        return lbl;
    }

    static string GetAwardClass(string awardType) => awardType switch
    {
        "mvp" => "trajectory-award-badge--mvp",
        "finals_mvp" => "trajectory-award-badge--finals_mvp",
        "champion" => "trajectory-award-badge--champion",
        "all_star" => "trajectory-award-badge--all_star",
        "first_team" => "trajectory-award-badge--first_team",
        "second_team" => "trajectory-award-badge--second_team",
        "roty" => "trajectory-award-badge--roty",
        "dpoy" => "trajectory-award-badge--dpoy",
        "sixth_man" => "trajectory-award-badge--sixth_man",
        "mip" => "trajectory-award-badge--mip",
        "player_month" => "trajectory-award-badge--player_month",
        "rookie_month" => "trajectory-award-badge--rookie_month",
        _ => ""
    };

    static string GetAwardText(PlayerAwardEntry award)
    {
        string label = award.award_type switch
        {
            "mvp" => "MVP",
            "finals_mvp" => "MVP Finales",
            "champion" => "Campeón",
            "all_star" => "All-Star",
            "first_team" => "1er Quinteto",
            "second_team" => "2º Quinteto",
            "roty" => "Rookie del Año",
            "dpoy" => "Mejor Defensor",
        "sixth_man" => "Sexto Hombre",
        "mip" => "Jugador + Mejorado",
        "player_month" => "Jugador del Mes",
        "rookie_month" => "Rookie del Mes",
        _ => award.award_type
        };
        return $"{label} ({award.year_start}-{award.year_end})";
    }

    protected override void RefreshHeader()
    {
        if (_manager == null || _season == null) return;

        var team = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        if (team != null)
        {
            if (_headerTeamLogo != null && !string.IsNullOrEmpty(team.logo))
            {
                var logoSprites = Resources.LoadAll<Sprite>($"Logos/{team.logo}");
                if (logoSprites != null && logoSprites.Length > 0)
                    _headerTeamLogo.style.backgroundImage = new StyleBackground(logoSprites[0]);
            }
            if (_headerTeamName != null)
                _headerTeamName.text = team.name;
        }
        if (_headerManagerName != null)
            _headerManagerName.text = _manager.name;

        if (_headerBudget != null || _headerMargin != null)
        {
            var teamPlayers = DatabaseManager.Instance.GetPlayersByTeam(_manager.team_id);
            long totalPayroll = teamPlayers.Sum(p => p.salary);
            var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
            long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
            long margin = salaryCap - totalPayroll;

            if (_headerBudget != null)
                _headerBudget.text = $"{totalPayroll / 1_000_000}M";
            if (_headerPayroll != null)
                _headerPayroll.text = $"Tope: ${salaryCap / 1_000_000}M";
            if (_headerMargin != null)
            {
                string marginText = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
                _headerMargin.text = marginText;
                _headerMargin.RemoveFromClassList("header-stat-value--negative");
                if (margin < 0) _headerMargin.AddToClassList("header-stat-value--negative");
            }
        }
        if (_headerChemistry != null)
        {
            int chemistry = DatabaseManager.Instance.GetTeamChemistry(_manager.team_id);
            _headerChemistry.text = $"{chemistry}%";
            _headerChemistry.RemoveFromClassList("header-stat-value--gold");
            _headerChemistry.RemoveFromClassList("header-stat-value--negative");
            if (chemistry < 40)
                _headerChemistry.AddToClassList("header-stat-value--negative");
            else if (chemistry < 70)
                _headerChemistry.AddToClassList("header-stat-value--gold");
        }
        if (_headerSeason != null)
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
        if (_headerDate != null)
            _headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
    }
}
