using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class TrajectoryController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerChemistry;
    private Button _btnAction;
    private Label _headerSeason;
    private Label _headerDate;

    // Cabecera jugador
    private VisualElement _trajectoryPhoto;
    private Label _trajectoryPlayerName;
    private Label _trajectoryPlayerPos;
    private Label _trajectoryRoleName;
    private VisualElement _trajectoryRoleIcon;
    private Label _trajectoryMeta;
    private Label _trajectoryNationality;
    private Label _trajectoryOvr;

    // Logros
    private VisualElement _trajectoryAwards;
    private VisualElement _trajectoryAwardsBody;

    // Tabla
    private VisualElement _trajectoryTableBody;
    private VisualElement _trajectoryEmpty;
    private VisualElement _trajectoryStatsSection;

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
    private VisualElement _configMainMenuConfirmOverlay;
    private Button _configBtnMainMenu;
    private Button _configBtnMainMenuYes;
    private Button _configBtnMainMenuNo;
    private VisualElement _configExitConfirmOverlay;
    private Button _configBtnExit;
    private Button _configBtnExitYes;
    private Button _configBtnExitNo;

    private PlayerData _player;
    private ManagerData _manager;
    private SeasonData _season;
    private List<PlayerCareerSeasonRow> _careerHistory;
    private List<PlayerAwardEntry> _awards;

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
        _trajectoryPhoto = _root.Q<VisualElement>("TrajectoryPhoto");
        _trajectoryPlayerName = _root.Q<Label>("TrajectoryPlayerName");
        _trajectoryPlayerPos = _root.Q<Label>("TrajectoryPlayerPos");
        _trajectoryRoleName = _root.Q<Label>("TrajectoryRoleName");
        _trajectoryRoleIcon = _root.Q<VisualElement>("TrajectoryRoleIcon");
        _trajectoryMeta = _root.Q<Label>("TrajectoryMeta");
        _trajectoryNationality = _root.Q<Label>("TrajectoryNationality");
        _trajectoryOvr = _root.Q<Label>("TrajectoryOvr");
        _trajectoryAwards = _root.Q<VisualElement>("TrajectoryAwards");
        _trajectoryAwardsBody = _root.Q<VisualElement>("TrajectoryAwardsBody");
        _trajectoryTableBody = _root.Q<VisualElement>("TrajectoryTableBody");
        _trajectoryEmpty = _root.Q<VisualElement>("TrajectoryEmpty");
        _trajectoryStatsSection = _root.Q<VisualElement>("TrajectoryStatsSection");

        var scrollView = _root.Q<ScrollView>();
        if (scrollView != null)
            scrollView.contentContainer.style.flexGrow = 0;

        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerChemistry = _root.Q<Label>("HeaderChemistry");
        _btnAction = _root.Q<Button>("BtnAction");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
    }

    void LoadSidebarIcons()
    {
        var iconMap = new Dictionary<string, string>
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

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);

        int playerId = ScreenManager.SelectedPlayerId;
        _player = DatabaseManager.Instance.GetPlayerById(playerId);
        if (_player == null) return;

        _careerHistory = DatabaseManager.Instance.GetPlayerCareerHistory(playerId, _manager.id);
        _awards = DatabaseManager.Instance.GetPlayerAwards(playerId);
    }

    void RegisterCallbacks()
    {
        SidebarController.Attach(_root, GameScreen.Trajectory);
        HeaderController.Attach(_root);
        RegisterNavButtons();

        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

        _root.Q<Button>("TrajectoryBackBtn")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Roster); });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(_root.Q<Button>("TrajectoryBackBtn"));
    }

    void RegisterNavButtons()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
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
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Market); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Finances); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavManager")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Manager); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OpenConfigModal(); });
    }

    void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Trajectory] RefreshHeader error: {ex.Message}"); }
        if (_player == null) return;
        FillPlayerHeader();
        BuildAwards();
        BuildStatsTable();
    }

    void FillPlayerHeader()
    {
        _trajectoryPlayerName.text = $"{_player.first_name} {_player.last_name}".ToUpper();
        _trajectoryPlayerPos.text = $"{PositionCodes.GetName(_player.position)} · {PositionCodes.GetName(_player.secondary_position)}";

        UpdateRoleIcon(_trajectoryRoleIcon, _player.role);
        _trajectoryRoleName.text = GetRoleName(_player.role);

        _trajectoryMeta.text = $"{_player.age} años · {_player.height_cm / 100f:F2}m · {_player.weight_kg}kg";
        _trajectoryNationality.text = CountryCodes.GetName(_player.nationality);

        int ovr = _player.GetCalculatedAverage();
        _trajectoryOvr.text = ovr.ToString();
        _trajectoryOvr.RemoveFromClassList("trajectory-ovr--high");
        _trajectoryOvr.RemoveFromClassList("trajectory-ovr--mid");
        _trajectoryOvr.RemoveFromClassList("trajectory-ovr--low");
        if (ovr > 84)
            _trajectoryOvr.AddToClassList("trajectory-ovr--high");
        else if (ovr >= 70)
            _trajectoryOvr.AddToClassList("trajectory-ovr--mid");
        else
            _trajectoryOvr.AddToClassList("trajectory-ovr--low");

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

        foreach (var season in _careerHistory)
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

        // Fila de totales carrera (calculados desde careerHistory)
        int totalGames = _careerHistory.Sum(s => s.games);
        double totalMinutes = _careerHistory.Sum(s => s.total_minutes);
        int totalPts = _careerHistory.Sum(s => s.total_points);
        int totalReb = _careerHistory.Sum(s => s.total_rebounds);
        int totalAst = _careerHistory.Sum(s => s.total_assists);
        int totalStl = _careerHistory.Sum(s => s.total_steals);
        int totalBlk = _careerHistory.Sum(s => s.total_blocks);
        int totalRat = _careerHistory.Sum(s => s.total_rating);

        var totalRow = new VisualElement();
        totalRow.AddToClassList("trajectory-table-row");
        totalRow.AddToClassList("trajectory-table-row--totals");

        totalRow.Add(CreateCell("td-season", "TOTAL"));
        totalRow.Add(CreateCell("td-team", ""));
        totalRow.Add(CreateCell("td-gp", totalGames.ToString()));
        totalRow.Add(CreateCell("td-mp", totalGames > 0
            ? (totalMinutes / totalGames).ToString("F1", fmt) : "0.0"));
        totalRow.Add(CreateCell("td-pts", totalGames > 0
            ? ((float)totalPts / totalGames).ToString("F1", fmt) : "0.0"));
        totalRow.Add(CreateCell("td-reb", totalGames > 0
            ? ((float)totalReb / totalGames).ToString("F1", fmt) : "0.0"));
        totalRow.Add(CreateCell("td-ast", totalGames > 0
            ? ((float)totalAst / totalGames).ToString("F1", fmt) : "0.0"));
        totalRow.Add(CreateCell("td-stl", totalGames > 0
            ? ((float)totalStl / totalGames).ToString("F1", fmt) : "0.0"));
        totalRow.Add(CreateCell("td-blk", totalGames > 0
            ? ((float)totalBlk / totalGames).ToString("F1", fmt) : "0.0"));
        totalRow.Add(CreateCell("td-val", totalGames > 0
            ? ((float)totalRat / totalGames).ToString("F1", fmt) : "0.0"));

        _trajectoryTableBody.Add(totalRow);
    }

    static Label CreateCell(string className, string text)
    {
        var lbl = new Label();
        lbl.AddToClassList(className);
        lbl.text = text;
        return lbl;
    }

    static string GetRoleName(PlayerRole role) => role switch
    {
        PlayerRole.Estrella => "Estrella",
        PlayerRole.Titular => "Titular",
        PlayerRole.Banquillo => "Banquillo",
        _ => "Último recurso"
    };

    void UpdateRoleIcon(VisualElement icon, PlayerRole role)
    {
        string iconName = role switch
        {
            PlayerRole.Estrella => "rol_estrella",
            PlayerRole.Titular => "rol_titular",
            PlayerRole.Banquillo => "rol_banquillo",
            _ => "rol_ultimoRecurso"
        };
        var tex = Resources.Load<Texture2D>($"Icons/{iconName}");
        icon.style.backgroundImage = tex != null ? new StyleBackground(tex) : StyleKeyword.None;
        icon.tooltip = GetRoleName(role);
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

    // ── CONFIG MODAL ──

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
                CloseConfigModal();
        });

        _configBtnMainMenu?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenMainMenuConfirm(); });
        _configBtnMainMenuNo?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseMainMenuConfirm(); });
        _configBtnMainMenuYes?.RegisterCallback<ClickEvent>(_ => { PlayClick(); GoToMainMenu(); });
        _configBtnExit?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenExitConfirm(); });
        _configBtnExitNo?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseExitConfirm(); });
        _configBtnExitYes?.RegisterCallback<ClickEvent>(_ => { PlayClick(); QuitGame(); });
    }

    void OpenConfigModal()
    {
        if (_configModalOverlay == null) return;
        _configModalOverlay.style.display = DisplayStyle.Flex;
        _configSliderMaster.SetValueWithoutNotify(AudioManager.Instance?.MasterVolume ?? 1f);
        _configSliderMusic.SetValueWithoutNotify(AudioManager.Instance?.MusicVolume ?? 1f);
        _configSliderSFX.SetValueWithoutNotify(AudioManager.Instance?.SFXVolume ?? 1f);
        int q = QualitySettings.GetQualityLevel();
        SelectConfigQuality(q, true);
        UpdateConfigLabels();
    }

    void CloseConfigModal()
    {
        if (_configModalOverlay != null)
            _configModalOverlay.style.display = DisplayStyle.None;
        CloseMainMenuConfirm();
        CloseExitConfirm();
    }

    void UpdateConfigLabels()
    {
        if (_configLabelMaster != null) _configLabelMaster.text = $"{Mathf.RoundToInt(_configSliderMaster.Value * 100)}%";
        if (_configLabelMusic != null)  _configLabelMusic.text  = $"{Mathf.RoundToInt(_configSliderMusic.Value * 100)}%";
        if (_configLabelSFX != null)    _configLabelSFX.text    = $"{Mathf.RoundToInt(_configSliderSFX.Value * 100)}%";
    }

    void SelectConfigQuality(int level, bool silent = false)
    {
        QualitySettings.SetQualityLevel(level, true);
        string activeClass = "settings-quality-btn--active";
        _configBtnQualityLow?.RemoveFromClassList(activeClass);
        _configBtnQualityMedium?.RemoveFromClassList(activeClass);
        _configBtnQualityHigh?.RemoveFromClassList(activeClass);
        _configBtnQualityUltra?.RemoveFromClassList(activeClass);
        var map = new[] { _configBtnQualityLow, _configBtnQualityMedium, _configBtnQualityHigh, _configBtnQualityUltra };
        if (level >= 0 && level < map.Length && map[level] != null)
            map[level].AddToClassList(activeClass);
    }

    void OpenMainMenuConfirm()
    {
        if (_configMainMenuConfirmOverlay != null)
            _configMainMenuConfirmOverlay.style.display = DisplayStyle.Flex;
    }

    void CloseMainMenuConfirm()
    {
        if (_configMainMenuConfirmOverlay != null)
            _configMainMenuConfirmOverlay.style.display = DisplayStyle.None;
    }

    void GoToMainMenu()
    {
        CloseConfigModal();
        ScreenManager.Instance.GoTo(GameScreen.MainMenu);
    }

    void OpenExitConfirm()
    {
        if (_configExitConfirmOverlay != null)
            _configExitConfirmOverlay.style.display = DisplayStyle.Flex;
    }

    void CloseExitConfirm()
    {
        if (_configExitConfirmOverlay != null)
            _configExitConfirmOverlay.style.display = DisplayStyle.None;
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void RefreshHeader()
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

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
