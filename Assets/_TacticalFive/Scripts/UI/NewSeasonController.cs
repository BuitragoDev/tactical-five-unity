using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class NewSeasonController : UIScreenController
{
    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerSeason;
    private Label _headerDate;

    // Content
    private Label _gameModeTag;
    private VisualElement _teamSelection;
    private Label _noteText;
    private Button _btnStartSeason;

    private Dictionary<string, Sprite> _logo52;
    private int _selectedTeamId;
    private List<TeamData> _availableTeams;

    // Roster modal
    private VisualElement _rosterOverlay;
    private Label _rosterCountLabel;
    private Button _btnRosterContinue;
    private List<PlayerData> _rosterPlayers;
    private int _rosterActiveCount;

    // Team option modal
    private VisualElement _teamOptionOverlay;
    private List<PlayerData> _pendingOptionPlayers;

    // Re-sign candidates (jugadores que declinaron su player option y son FA reciente)
    private List<PlayerData> _reSignCandidates = new();

    // Summer League / camp de desarrollo post-draft
    private VisualElement _summerLeagueOverlay;
    private readonly HashSet<int> _summerLeagueSelected = new();
    private bool _summerLeagueResolved;

    private struct PlayerOptionResult
    {
        public string firstName;
        public string lastName;
        public string position;
        public long salary;
        public bool optedIn;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        _rosterOverlay = new VisualElement();
        _rosterOverlay.AddToClassList("ns-roster-overlay");
        _root.Add(_rosterOverlay);

        _teamOptionOverlay = new VisualElement();
        _teamOptionOverlay.AddToClassList("ns-roster-overlay");
        _root.Add(_teamOptionOverlay);

        _summerLeagueOverlay = new VisualElement();
        _summerLeagueOverlay.AddToClassList("ns-roster-overlay");
        _root.Add(_summerLeagueOverlay);

        CursorManager.Instance?.SetDefaultCursor();
    }

    protected override void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _gameModeTag = _root.Q<Label>("GameModeTag");
        _teamSelection = _root.Q<VisualElement>("TeamSelection");
        _noteText = _root.Q<Label>("NoteText");
        _btnStartSeason = _root.Q<Button>("BtnStartSeason");
    }

    protected override void LoadData()
    {
        base.LoadData();
        if (_season == null) return;

        var logos = TeamImages.Logos(64);
        _logo52 = new Dictionary<string, Sprite>();
        foreach (var s in logos) _logo52[s.name] = s;
    }

    protected override void RegisterCallbacks()
    {
        _btnStartSeason?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnStartSeason(); });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(_btnStartSeason);
    }

    protected override void Refresh()
    {
        if (_season == null || _myTeam == null) return;
        RefreshHeader();
        RefreshContent();
    }

    protected override void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logos64 = TeamImages.Logos(64);
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos64) logoDict[s.name] = s;
        if (logoDict.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = LocalizationService.T("ui.common.manager", _manager.name);
        _headerSeason.text = LocalizationService.T("ui.common.season", _season.year_start, _season.year_end);
        if (!string.IsNullOrEmpty(_season.current_date))
        {
            if (DateTime.TryParse(_season.current_date, out var dt))
                _headerDate.text = dt.ToString("dd/MM/yyyy");
        }
    }

    void RefreshContent()
    {
        string modeName = _season.game_mode == "promanager" ? "ProManager" : "Manager";
        _gameModeTag.text = modeName;

        LoadAvailableTeams();
        BuildTeamCards();

        if (_season.game_mode == "promanager")
            _noteText.text = LocalizationService.T("ui.code.new_season_controller.selecciona_tu_equipo_actual_o_uno_de_los_0cfc8f9e");
        else
            _noteText.text = LocalizationService.T("ui.code.new_season_controller.continuaras_con_tu_equipo_actual_en_la_n_f29723fc");
    }

    void LoadAvailableTeams()
    {
        _availableTeams = new List<TeamData>();
        _selectedTeamId = _myTeam.id;

        if (_season.game_mode == "manager")
        {
            _availableTeams.Add(_myTeam);
            return;
        }

        // ProManager: current team + 3 random from bottom 10 (excluding current)
        var standings = GetTeamStandings();
        var bottom10 = standings.OrderBy(s => s.wins).Take(10).ToList();
        bottom10 = bottom10.Where(s => s.teamId != _myTeam.id).ToList();

        System.Random rng = new System.Random();
        var random3 = bottom10.OrderBy(_ => rng.Next()).Take(3).ToList();

        _availableTeams.Add(_myTeam);
        foreach (var st in random3)
        {
            var team = DatabaseManager.Instance.GetTeamById(st.teamId);
            if (team != null)
                _availableTeams.Add(team);
        }
    }

    List<(int teamId, int wins, int losses)> GetTeamStandings()
    {
        var games = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        var teams = DatabaseManager.Instance.GetAllTeams();
        var standings = teams.ToDictionary(t => t.id, t => (teamId: t.id, wins: 0, losses: 0));

        foreach (var g in games)
        {
            if (standings.ContainsKey(g.home_team_id))
            {
                var h = standings[g.home_team_id];
                if (g.home_score > g.away_score) h.wins++;
                else h.losses++;
                standings[g.home_team_id] = h;
            }
            if (standings.ContainsKey(g.away_team_id))
            {
                var a = standings[g.away_team_id];
                if (g.away_score > g.home_score) a.wins++;
                else a.losses++;
                standings[g.away_team_id] = a;
            }
        }

        var rows = standings.Values.ToList();
        rows.Sort((a, b) =>
        {
            float pctA = a.wins + a.losses > 0 ? (float)a.wins / (a.wins + a.losses) : 0;
            float pctB = b.wins + b.losses > 0 ? (float)b.wins / (b.wins + b.losses) : 0;
            if (pctB != pctA) return pctB.CompareTo(pctA);
            if (a.losses != b.losses) return a.losses.CompareTo(b.losses);
            return b.wins.CompareTo(a.wins);
        });

        return rows;
    }

    void BuildTeamCards()
    {
        _teamSelection.Clear();

        foreach (var team in _availableTeams)
        {
            var card = new VisualElement();
            card.AddToClassList("team-select-card");
            if (team.id == _selectedTeamId)
                card.AddToClassList("selected");

            var logo = new VisualElement();
            logo.AddToClassList("team-select-logo");
            if (_logo52.TryGetValue(team.logo, out var sprite))
                logo.style.backgroundImage = new StyleBackground(sprite);
            card.Add(logo);

            var info = new VisualElement();
            info.AddToClassList("team-select-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("team-select-name");
            nameLbl.text = team.name;
            info.Add(nameLbl);

            string confName = team.conference == "East" ? "Conferencia Este" : "Conferencia Oeste";
            string detail = $"{team.city} · {confName}";
            if (team.id == _myTeam.id) detail += " · TU EQUIPO";
            var detailLbl = new Label();
            detailLbl.AddToClassList("team-select-detail");
            detailLbl.text = detail;
            info.Add(detailLbl);

            card.Add(info);

            if (team.id == _selectedTeamId)
            {
                var badge = new Label();
                badge.AddToClassList("team-select-badge");
                badge.text = LocalizationService.T("ui.code.new_season_controller.seleccionado");
                card.Add(badge);
            }

            int capturedId = team.id;
            card.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectTeam(capturedId); });

            CursorManager.Instance?.RegisterHandCursor(card);

            _teamSelection.Add(card);
        }
    }

    void SelectTeam(int teamId)
    {
        _selectedTeamId = teamId;
        // Rebuild cards to reflect selection
        BuildTeamCards();
    }

    void OnStartSeason()
    {
        // El control de plantilla se hace DESPUÉS de resolver opciones y de
        // decrementar contratos (CheckRosterAndStart), para que los jugadores
        // en último año no renovados dejen de pertenecer al equipo antes.
        CheckTeamOptions();
    }

    void CheckTeamOptions()
    {
        // Si las pre-condiciones del rollover ya se aplicaron (reintento tras un fallo
        // de StartNewSeason), no volver a resolver opciones ni decrementar contratos:
        // se duplicarían. Se salta directamente al control de plantilla.
        if (_season != null && DatabaseManager.Instance.IsRolloverPreApplied(_season.id, "pre"))
        {
            CheckRosterAndStart();
            return;
        }

        var players = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeamId);
        var teamOptions = players
            .Where(p => p.has_team_option == 1 && p.guaranteed_years == 0 && p.contract_years > 0)
            .OrderByDescending(p => p.salary)
            .ToList();

        if (teamOptions.Count > 0)
        {
            OpenTeamOptionModal(teamOptions);
            return;
        }

        ResolvePlayerOptionsAndStart();
    }

    void ResolvePlayerOptionsAndStart()
    {
        var players = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeamId);
        var playerOptions = players
            .Where(p => p.has_player_option == 1 && p.guaranteed_years == 0 && p.contract_years > 0)
            .OrderByDescending(p => p.salary)
            .ToList();

        if (playerOptions.Count == 0)
        {
            CheckRosterAndStart();
            return;
        }

        float teamWinPct = 0.5f;
        var settings = DatabaseManager.Instance.GetLeagueSettings();
        if (_season != null)
        {
            var seasonGames = DatabaseManager.Instance.GetSeasonGames(_manager.id, _season.id);
            int total = seasonGames.Count;
            if (total > 0)
            {
                int wins = seasonGames.Count(g =>
                    (g.home_team_id == _selectedTeamId && g.home_score > g.away_score) ||
                    (g.away_team_id == _selectedTeamId && g.away_score > g.home_score));
                teamWinPct = (float)wins / total;
            }
        }

        var results = new List<PlayerOptionResult>();
        _reSignCandidates.Clear();
        foreach (var p in playerOptions)
        {
            bool wasTwoWay = p.is_two_way == 1;
            bool optsIn = DatabaseManager.Instance.DecidePlayerOption(p, teamWinPct, settings);
            if (optsIn)
            {
                p.contract_years += 1;
                p.guaranteed_years = p.contract_years;
            }
            else
            {
                p.years_of_service++;
                p.contract_years = 0;
                p.guaranteed_years = 0;
                if (p.last_team_id == 0) p.last_team_id = _selectedTeamId;
                p.team_id = 0;
                p.is_two_way = 0;
                p.is_on_ir = 0;
                p.g_league_assigned = 0;
                _reSignCandidates.Add(p);
                if (!wasTwoWay)
                {
                    long minimum = TradeHelper.MinimumSalary(settings, p.years_of_service);
                    DatabaseManager.Instance.SaveCapHold(p, _selectedTeamId, _season?.id ?? 0, minimum);
                }
            }
            p.has_player_option = 0;
            DatabaseManager.Instance.UpdatePlayer(p);

            results.Add(new PlayerOptionResult
            {
                firstName = p.first_name,
                lastName = p.last_name,
                position = p.position,
                salary = p.salary,
                optedIn = optsIn
            });
        }

        OpenPlayerOptionResultModal(results);
    }

    void CheckRosterAndStart()
    {
        // Decrementar contratos de la plantilla seleccionada para que los jugadores
        // en último año de contrato no renovados pasen a agentes libres ANTES del
        // control de plantilla (>16 estándar o >2 two-way; 18 activos). Misma semántica que StartNewSeason.
        // Si ya se aplicó (reintento tras fallo del rollover) se omite: la marca
        // "pre" cubre opciones + decremento y evita descontar dos veces.
        bool preApplied = _season != null && DatabaseManager.Instance.IsRolloverPreApplied(_season.id, "pre");
        if (!preApplied)
        {
            var prePlayers = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeamId);
            foreach (var p in prePlayers)
            {
                // B41: el contrato de rookie no decrementa en su primer año (su edad y
                // progresión las aplica StartNewSeason). Mismo criterio en ambas rutas.
                int oldTeamId = p.team_id;
                bool wasTwoWay = p.is_two_way == 1;
                if (oldTeamId != 0) p.years_of_service++;
                if (!RolloverRules.ShouldDecrementContract(p.is_rookie == 1))
                {
                    DatabaseManager.Instance.UpdatePlayer(p);
                    continue;
                }
                p.contract_years = System.Math.Max(0, p.contract_years - 1);
                p.guaranteed_years = System.Math.Max(0, p.guaranteed_years - 1);
                if (p.contract_years <= 0)
                {
                    p.contract_years = 0;
                    p.guaranteed_years = 0;
                    p.has_team_option = 0;
                    p.has_player_option = 0;
                    p.is_two_way = 0;
                    p.is_on_ir = 0;
                    p.g_league_assigned = 0;
                    if (oldTeamId != 0 && p.last_team_id == 0)
                        p.last_team_id = oldTeamId; // conserva Bird rights/contexto de re-firma al ir a FA
                    p.team_id = 0; // agente libre
                    if (oldTeamId != 0 && !wasTwoWay)
                    {
                        var settings = DatabaseManager.Instance.GetLeagueSettings();
                        long minSalary = TradeHelper.MinimumSalary(settings, p.years_of_service);
                        DatabaseManager.Instance.SaveCapHold(p, oldTeamId, _season?.id ?? 0, minSalary);
                    }
                }
                else
                {
                    p.seasons_with_team += 1; // sigue en el mismo equipo
                }
                DatabaseManager.Instance.UpdatePlayer(p);
            }

            if (_season != null)
                DatabaseManager.Instance.MarkRolloverPreApplied(_season.id, "pre");
        }

        var roster = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeamId);
        if (DatabaseManager.Instance.GetRosterCount(_selectedTeamId) > TradeHelper.MAX_STANDARD_ROSTER
            || DatabaseManager.Instance.GetTwoWayCount(_selectedTeamId) > TradeHelper.MAX_TWO_WAY)
        {
            OpenRosterModal(roster.Where(p => p.is_on_ir == 0).ToList());
            return;
        }
        BeginSummerLeagueOrStart();
    }

    void BeginSummerLeagueOrStart()
    {
        if (_summerLeagueResolved
            || DatabaseManager.Instance.HasSummerLeagueResults(_manager.id, _season.id))
        {
            _summerLeagueResolved = true;
            ExecuteStartSeason();
            return;
        }
        OpenSummerLeagueModal();
    }

    void OpenSummerLeagueModal()
    {
        var candidates = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeamId)
            .Where(SummerLeagueHelper.IsEligible)
            .OrderByDescending(p => p.potential)
            .ThenByDescending(p => p.GetCalculatedAverage())
            .ToList();

        if (candidates.Count == 0)
        {
            _summerLeagueResolved = true;
            ExecuteStartSeason();
            return;
        }

        _summerLeagueSelected.Clear();
        foreach (var p in SummerLeagueHelper.DefaultSelection(candidates))
            _summerLeagueSelected.Add(p.id);

        _summerLeagueOverlay.Clear();
        _summerLeagueOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("ns-roster-box");
        _summerLeagueOverlay.Add(box);

        var title = new Label(LocalizationService.T("ui.code.new_season_controller.summer_league_camp_de_desarrollo"));
        title.AddToClassList("ns-roster-title");
        box.Add(title);

        var subtitle = new Label($"Ventana post-draft: 3 partidos para rookies y sophomores. " +
                                 $"Elige hasta {SummerLeagueHelper.MaxRoster} convocados. " +
                                 "El desarrollo es acotado: como máximo +1 en un atributo y nunca supera el potencial.");
        subtitle.AddToClassList("ns-roster-subtitle");
        box.Add(subtitle);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.maxHeight = 380;
        scroll.style.marginTop = 8;
        scroll.style.marginBottom = 8;
        box.Add(scroll);

        foreach (var p in candidates)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.22f, 0.24f, 0.30f);

            var info = new Label($"{p.first_name} {p.last_name} · {p.position} · {p.age} años · " +
                                 $"MED {p.overall} / POT {p.potential}");
            info.style.color = new Color(0.82f, 0.85f, 0.92f);
            info.style.fontSize = 14;
            row.Add(info);

            var toggle = new Button();
            toggle.AddToClassList("ns-option-btn");
            toggle.style.minWidth = 120;
            toggle.text = _summerLeagueSelected.Contains(p.id) ? "CONVOCADO" : "FUERA";
            toggle.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                if (_summerLeagueSelected.Contains(p.id))
                    _summerLeagueSelected.Remove(p.id);
                else if (_summerLeagueSelected.Count < SummerLeagueHelper.MaxRoster)
                    _summerLeagueSelected.Add(p.id);
                toggle.text = _summerLeagueSelected.Contains(p.id) ? "CONVOCADO" : "FUERA";
            });
            CursorManager.Instance?.RegisterHandCursor(toggle);
            row.Add(toggle);
            scroll.Add(row);
        }

        var actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.marginTop = 8;
        box.Add(actions);

        var skip = new Button();
        skip.AddToClassList("ns-roster-continue");
        skip.style.backgroundColor = new Color(0.55f, 0.18f, 0.15f);
        skip.text = LocalizationService.T("ui.code.new_season_controller.saltar_camp");
        skip.style.marginRight = 8;
        skip.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _summerLeagueResolved = true;
            _summerLeagueOverlay.style.display = DisplayStyle.None;
            ExecuteStartSeason();
        });
        CursorManager.Instance?.RegisterHandCursor(skip);
        actions.Add(skip);

        var simulate = new Button();
        simulate.AddToClassList("ns-roster-continue");
        simulate.text = LocalizationService.T("ui.code.new_season_controller.simular_summer_league");
        simulate.SetEnabled(_summerLeagueSelected.Count > 0);
        simulate.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            RunSummerLeague(candidates);
        });
        CursorManager.Instance?.RegisterHandCursor(simulate);
        actions.Add(simulate);
    }

    void RunSummerLeague(List<PlayerData> candidates)
    {
        var selected = candidates.Where(p => _summerLeagueSelected.Contains(p.id)).ToList();
        if (selected.Count == 0) return;

        int seed = (_season?.id ?? 0) * 397 + (_manager?.id ?? 0) * 17 + selected.Count;
        var results = SummerLeagueHelper.Simulate(selected, seed);

        foreach (var row in results)
        {
            var player = selected.FirstOrDefault(p => p.id == row.player_id);
            if (player == null) continue;
            bool applied = row.development_gain > 0
                           && SummerLeagueHelper.ApplyDevelopment(player, row.development_gain);
            if (!applied) row.development_gain = 0;
            if (applied) DatabaseManager.Instance.UpdatePlayer(player);
        }

        DatabaseManager.Instance.SaveSummerLeagueResults(
            _manager.id, _season.id, _selectedTeamId, results);
        ShowSummerLeagueSummary(results);
    }

    void ShowSummerLeagueSummary(List<SummerLeagueStatData> results)
    {
        _summerLeagueOverlay.Clear();
        _summerLeagueOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("ns-roster-box");
        _summerLeagueOverlay.Add(box);

        var title = new Label(LocalizationService.T("ui.code.new_season_controller.summer_league_completada"));
        title.AddToClassList("ns-roster-title");
        box.Add(title);

        var subtitle = new Label(LocalizationService.T("ui.code.new_season_controller.resumen_del_camp_los_jugadores_con_minut_8c87d326"));
        subtitle.AddToClassList("ns-roster-subtitle");
        box.Add(subtitle);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.maxHeight = 380;
        scroll.style.marginTop = 8;
        box.Add(scroll);

        foreach (var row in results.OrderByDescending(r => r.development_gain).ThenByDescending(r => r.points))
        {
            var player = DatabaseManager.Instance.GetPlayerById(row.player_id);
            if (player == null) continue;
            string gain = row.development_gain > 0 ? " · +1 ATRIBUTO" : "";
            var line = new Label(
                $"{player.first_name} {player.last_name} · {row.games} PJ · {row.minutes} min · " +
                $"{row.points} pts · {row.rebounds} reb · {row.assists} as{gain}");
            line.style.color = row.development_gain > 0
                ? new Color(0.45f, 0.88f, 0.55f)
                : new Color(0.82f, 0.85f, 0.92f);
            line.style.fontSize = 14;
            line.style.paddingTop = 4;
            line.style.paddingBottom = 4;
            scroll.Add(line);
        }

        var continueBtn = new Button();
        continueBtn.AddToClassList("ns-roster-continue");
        continueBtn.text = LocalizationService.T("ui.code.new_season_controller.continuar_a_la_temporada");
        continueBtn.style.marginTop = 10;
        continueBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _summerLeagueResolved = true;
            _summerLeagueOverlay.style.display = DisplayStyle.None;
            ExecuteStartSeason();
        });
        CursorManager.Instance?.RegisterHandCursor(continueBtn);
        box.Add(continueBtn);
    }

    void ExecuteStartSeason()
    {
        _btnStartSeason.SetEnabled(false);
        _btnStartSeason.text = LocalizationService.T("ui.code.new_season_controller.iniciando");
        StartCoroutine(ExecuteStartSeasonAsync());
    }

    IEnumerator ExecuteStartSeasonAsync()
    {
        int newTeamId = _selectedTeamId;
        int oldTeamId = _manager.team_id; // Capture before possible update

        int seasonId = _season.id;
        int managerId = _manager.id;
        string gameMode = _season.game_mode;

        // Bloquear salidas duras (config → Menú principal) mientras corre el rollover:
        // si la corrutina se aborta a media transacción, el lote de fondo seguiría
        // escribiendo sin que el jugador lo sepa.
        ScreenManager.NavigationLocked = true;

        // El equipo del manager se fija DENTRO de la transacción de StartNewSeason
        // (antes se cambiaba aquí, fuera: si el rollover fallaba quedaba cambiado).

        // Execute the full new season logic on a background thread (own SQLite connection)
        var task = DatabaseManager.Instance.RunInBackgroundAsync(() =>
        {
            DatabaseManager.Instance.StartNewSeason(
                seasonId,
                newTeamId,
                gameMode,
                managerId,
                oldTeamId
            );
        });

        while (!task.IsCompleted)
            yield return null;

        try
        {
            task.GetAwaiter().GetResult();
        }
        catch (System.Exception ex)
        {
            ScreenManager.NavigationLocked = false;
            _btnStartSeason.SetEnabled(true);
            _btnStartSeason.text = LocalizationService.T("ui.code.new_season_controller.empezar_temporada");
            Debug.LogError($"[NewSeason] Error al iniciar la nueva temporada: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }

        ScreenManager.NavigationLocked = false;

        // Navigate to Preseason to set up friendly games
        ScreenManager.Instance.GoTo(GameScreen.Preseason);
    }

    // ── ROSTER TRIM MODAL ────────────────────────────────

    void OpenRosterModal(List<PlayerData> roster)
    {
        int standardExcess = Mathf.Max(0,
            DatabaseManager.Instance.GetRosterCount(_selectedTeamId) - TradeHelper.MAX_STANDARD_ROSTER);
        int twoWayExcess = Mathf.Max(0,
            DatabaseManager.Instance.GetTwoWayCount(_selectedTeamId) - TradeHelper.MAX_TWO_WAY);
        _rosterPlayers = roster
            .Where(p => p.is_two_way == 0 ? standardExcess > 0 : twoWayExcess > 0)
            .ToList();
        _rosterActiveCount = DatabaseManager.Instance.GetRosterCount(_selectedTeamId);

        _rosterOverlay.Clear();
        _rosterOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("ns-roster-box");
        _rosterOverlay.Add(box);

        var title = new Label(LocalizationService.T("ui.code.new_season_controller.plantilla_completa"));
        title.AddToClassList("ns-roster-title");
        box.Add(title);

        var subtitle = new Label($"La plantilla supera uno de sus límites. Libera contratos de la categoría excedida: estándar ({TradeHelper.MAX_STANDARD_ROSTER}) o two-way ({TradeHelper.MAX_TWO_WAY}).");
        subtitle.AddToClassList("ns-roster-subtitle");
        box.Add(subtitle);

        _rosterCountLabel = new Label($"Estándar: {_rosterActiveCount}/{TradeHelper.MAX_STANDARD_ROSTER} · Two-way: {DatabaseManager.Instance.GetTwoWayCount(_selectedTeamId)}/{TradeHelper.MAX_TWO_WAY}");
        _rosterCountLabel.AddToClassList("ns-roster-count");
        box.Add(_rosterCountLabel);

        var grid = new VisualElement();
        grid.AddToClassList("ns-roster-grid");
        box.Add(grid);

        var col1 = new VisualElement();
        col1.AddToClassList("ns-roster-col");
        var col2 = new VisualElement();
        col2.AddToClassList("ns-roster-col");
        var col3 = new VisualElement();
        col3.AddToClassList("ns-roster-col");
        var col4 = new VisualElement();
        col4.AddToClassList("ns-roster-col");
        grid.Add(col1);
        grid.Add(col2);
        grid.Add(col3);
        grid.Add(col4);

        int perCol = (int)System.Math.Ceiling(_rosterPlayers.Count / 4f);
        for (int i = 0; i < _rosterPlayers.Count; i++)
        {
            VisualElement target;
            if (i < perCol) target = col1;
            else if (i < perCol * 2) target = col2;
            else if (i < perCol * 3) target = col3;
            else target = col4;
            target.Add(BuildPlayerCard(_rosterPlayers[i]));
        }

        _btnRosterContinue = new Button();
        _btnRosterContinue.AddToClassList("ns-roster-continue");
        _btnRosterContinue.text = LocalizationService.T("ui.code.new_season_controller.continuar_a_la_siguiente_temporada");
        _btnRosterContinue.SetEnabled(_rosterActiveCount <= TradeHelper.MAX_STANDARD_ROSTER
            && DatabaseManager.Instance.GetTwoWayCount(_selectedTeamId) <= TradeHelper.MAX_TWO_WAY);
        _btnRosterContinue.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _rosterOverlay.style.display = DisplayStyle.None;
            // Las opciones ya se resolvieron antes del control de plantilla (CheckRosterAndStart)
            BeginSummerLeagueOrStart();
        });
        box.Add(_btnRosterContinue);

        CursorManager.Instance?.RegisterHandCursor(_btnRosterContinue);
    }

    VisualElement BuildPlayerCard(PlayerData p)
    {
        var card = new VisualElement();
        card.AddToClassList("ns-roster-card");

        var topRow = new VisualElement();
        topRow.AddToClassList("ns-roster-card-top");

        int ovr = p.GetCalculatedAverage();
        var medBox = new VisualElement();
        medBox.AddToClassList("ns-roster-med");
        if (ovr > 84)
            medBox.AddToClassList("ns-roster-med--high");
        else if (ovr >= 70)
            medBox.AddToClassList("ns-roster-med--mid");
        else
            medBox.AddToClassList("ns-roster-med--low");

        var medLbl = new Label();
        medLbl.AddToClassList("ns-roster-med-lbl");
        medLbl.text = LocalizationService.T("ui.code.new_season_controller.med");
        medBox.Add(medLbl);

        var medVal = new Label();
        medVal.AddToClassList("ns-roster-med-val");
        medVal.text = ovr.ToString();
        medBox.Add(medVal);

        topRow.Add(medBox);

        var rightCol = new VisualElement();
        rightCol.AddToClassList("ns-roster-card-right");

        var rookieTag = p.is_rookie == 1 ? " (R)" : "";
        var nameLbl = new Label($"{p.first_name} {p.last_name}{rookieTag}");
        nameLbl.AddToClassList("ns-roster-name");
        rightCol.Add(nameLbl);

        var detailRow = new VisualElement();
        detailRow.AddToClassList("ns-roster-detail");

        var salaryLbl = new Label($"{p.salary:N0}$");
        salaryLbl.AddToClassList("ns-roster-salary");
        detailRow.Add(salaryLbl);

        var yearsLbl = new Label();
        yearsLbl.AddToClassList("ns-roster-years");
        yearsLbl.text = p.contract_years == 1 ? "1 año" : $"{p.contract_years} años";
        detailRow.Add(yearsLbl);

        rightCol.Add(detailRow);
        topRow.Add(rightCol);
        card.Add(topRow);

        var btn = new Button();
        btn.AddToClassList("ns-roster-btn");
        btn.text = LocalizationService.T("ui.code.new_season_controller.rescindir_contrato");
        int capturedId = p.id;
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            OnRescindir(capturedId, btn);
        });
        CursorManager.Instance?.RegisterHandCursor(btn);
        card.Add(btn);

        return card;
    }

    void OnRescindir(int playerId, Button btn)
    {
        var player = _rosterPlayers.FirstOrDefault(p => p.id == playerId);
        if (player == null) return;
        if (player.team_id != _selectedTeamId) return;

        long salary = player.salary;
        int years = player.contract_years;
        long remainingSalary = salary * years;
        long penalty = (long)(remainingSalary * 0.5f);
        long netBalance = remainingSalary - penalty;
        int currentDay = _season?.current_game_day ?? 0;
        string playerName = $"{player.first_name} {player.last_name}";
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        player.team_id = 0;
        player.is_two_way = 0;
        player.is_on_ir = 0;
        DatabaseManager.Instance.UpdatePlayer(player);

        var selectedTeam = DatabaseManager.Instance.GetTeamById(_selectedTeamId);
        if (selectedTeam != null)
        {
            selectedTeam.budget -= penalty;
            DatabaseManager.Instance.UpdateTeamBudget(selectedTeam.id, selectedTeam.budget);
        }

        DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
        {
            team_id = _selectedTeamId,
            season_id = _season?.id ?? 0,
            record_type = FinanceRecord.TYPE_DISMISSAL,
            game_day = currentDay,
            amount = penalty
        });

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = "Jugador despedido",
            body = NewsTexts.Despedido(playerName, _myTeam.name) +
                   $"\n\nCoste de rescisión: {penalty:N0} $\nAhorro salarial restante: {remainingSalary:N0} $\nBalance neto de la operación: {netBalance:N0} $",
            game_day = currentDay,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = NewsType.DESPEDIDO
        });

        btn.text = LocalizationService.T("ui.code.new_season_controller.despedido");
        btn.SetEnabled(false);
        btn.AddToClassList("ns-roster-btn--dismissed");

        _rosterActiveCount = DatabaseManager.Instance.GetRosterCount(_selectedTeamId);
        _rosterCountLabel.text = $"Estándar: {_rosterActiveCount}/{TradeHelper.MAX_STANDARD_ROSTER} · Two-way: {DatabaseManager.Instance.GetTwoWayCount(_selectedTeamId)}/{TradeHelper.MAX_TWO_WAY}";

        bool withinLimits = _rosterActiveCount <= TradeHelper.MAX_STANDARD_ROSTER
            && DatabaseManager.Instance.GetTwoWayCount(_selectedTeamId) <= TradeHelper.MAX_TWO_WAY;
        if (withinLimits)
        {
            _btnRosterContinue.SetEnabled(true);
            CursorManager.Instance?.RegisterHandCursor(_btnRosterContinue);
        }
    }

    // ── TEAM OPTION DECISION MODAL ────────────────────────

    void OpenTeamOptionModal(List<PlayerData> players)
    {
        _pendingOptionPlayers = players;

        _teamOptionOverlay.Clear();
        _teamOptionOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("ns-roster-box");
        box.AddToClassList("ns-option-box");
        _teamOptionOverlay.Add(box);

        var title = new Label(LocalizationService.T("ui.code.new_season_controller.decisiones_de_contrato"));
        title.AddToClassList("ns-roster-title");
        box.Add(title);

        var subtitle = new Label(LocalizationService.T("ui.code.new_season_controller.los_siguientes_jugadores_tienen_una_team_bb4c121e"));
        subtitle.AddToClassList("ns-roster-subtitle");
        box.Add(subtitle);

        var list = new VisualElement();
        list.AddToClassList("ns-option-list");
        box.Add(list);

        // Track decisions: 0 = unset, 1 = exercise, -1 = decline
        var decisions = new Dictionary<int, int>();
        foreach (var p in players)
        {
            decisions[p.id] = 0;

            var row = new VisualElement();
            row.AddToClassList("ns-option-row");

            var info = new Label($"{p.first_name} {p.last_name} ({PositionCodes.GetName(p.position)})"
                + $" — {FormatMoney(p.salary)}"
                + $" — {p.contract_years} año{(p.contract_years != 1 ? "s" : "")} restantes");
            info.AddToClassList("ns-option-info");
            row.Add(info);

            var btns = new VisualElement();
            btns.AddToClassList("ns-option-btns");

            var exerciseBtn = new Button { text = LocalizationService.T("ui.code.new_season_controller.ejecutar") };
            exerciseBtn.AddToClassList("ns-option-btn");
            exerciseBtn.AddToClassList("ns-option-btn--exercise");
            CursorManager.Instance?.RegisterHandCursor(exerciseBtn);

            var declineBtn = new Button { text = LocalizationService.T("ui.code.new_season_controller.declinar") };
            declineBtn.AddToClassList("ns-option-btn");
            declineBtn.AddToClassList("ns-option-btn--decline");
            CursorManager.Instance?.RegisterHandCursor(declineBtn);

            exerciseBtn.clicked += () =>
            {
                decisions[p.id] = 1;
                declineBtn.SetEnabled(false);
                exerciseBtn.SetEnabled(false);
                exerciseBtn.AddToClassList("ns-option-btn--selected");
                CheckAllOptionsDecided(decisions);
            };
            declineBtn.clicked += () =>
            {
                decisions[p.id] = -1;
                exerciseBtn.SetEnabled(false);
                declineBtn.SetEnabled(false);
                declineBtn.AddToClassList("ns-option-btn--selected");
                CheckAllOptionsDecided(decisions);
            };

            btns.Add(exerciseBtn);
            btns.Add(declineBtn);
            row.Add(btns);
            list.Add(row);
        }
    }

    void CheckAllOptionsDecided(Dictionary<int, int> decisions)
    {
        if (decisions.Values.Any(v => v == 0)) return;

        foreach (var p in _pendingOptionPlayers)
        {
            if (!decisions.TryGetValue(p.id, out int dec)) continue;

            if (dec == 1)
            {
                p.contract_years += 1;
                p.guaranteed_years = p.contract_years;
                p.has_team_option = 0;
            }
            else
            {
                bool wasTwoWay = p.is_two_way == 1;
                p.years_of_service++;
                p.contract_years = 0;
                p.guaranteed_years = 0;
                p.has_team_option = 0;
                if (p.last_team_id == 0) p.last_team_id = _selectedTeamId;
                p.team_id = 0;
                if (!wasTwoWay)
                {
                    var settings = DatabaseManager.Instance.GetLeagueSettings();
                    long minimum = TradeHelper.MinimumSalary(settings, p.years_of_service);
                    DatabaseManager.Instance.SaveCapHold(p, _selectedTeamId, _season?.id ?? 0, minimum);
                }
            }
            DatabaseManager.Instance.UpdatePlayer(p);
        }

        _teamOptionOverlay.style.display = DisplayStyle.None;
        ResolvePlayerOptionsAndStart();
    }

    // ── PLAYER OPTION RESULT MODAL ────────────────────────

    void OpenPlayerOptionResultModal(List<PlayerOptionResult> results)
    {
        _teamOptionOverlay.Clear();
        _teamOptionOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("ns-roster-box");
        box.AddToClassList("ns-option-box");
        _teamOptionOverlay.Add(box);

        var title = new Label(LocalizationService.T("ui.code.new_season_controller.decisiones_de_jugador"));
        title.AddToClassList("ns-roster-title");
        box.Add(title);

        var subtitle = new Label(LocalizationService.T("ui.code.new_season_controller.los_siguientes_jugadores_tenian_player_o_0ad6377c"));
        subtitle.AddToClassList("ns-roster-subtitle");
        box.Add(subtitle);

        var list = new VisualElement();
        list.AddToClassList("ns-option-list");
        box.Add(list);

        foreach (var r in results)
        {
            var row = new VisualElement();
            row.AddToClassList("ns-option-row");

            string action = r.optedIn ? "EJERCE player option" : "RECHAZA player option (agente libre)";
            string color = r.optedIn ? "#27AE60" : "#C0392B";

            var info = new Label($"{r.firstName} {r.lastName} ({PositionCodes.GetName(r.position)})"
                + $" — {FormatMoney(r.salary)}"
                + $" — <color={color}>{action}</color>");
            info.AddToClassList("ns-option-info");
            info.enableRichText = true;
            row.Add(info);
            list.Add(row);
        }

        var continueBtn = new Button { text = LocalizationService.T("ui.code.new_season_controller.continuar") };
        continueBtn.AddToClassList("ns-roster-continue");
        continueBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _teamOptionOverlay.style.display = DisplayStyle.None;
            if (_reSignCandidates.Count > 0)
                OpenReSignModal();
            else
                CheckRosterAndStart();
        });
        box.Add(continueBtn);
        CursorManager.Instance?.RegisterHandCursor(continueBtn);
    }

    // ── RE-SIGN (re-firma de FA propio que declinó su opción) ────────

    void OpenReSignModal()
    {
        _teamOptionOverlay.Clear();
        _teamOptionOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("ns-roster-box");
        box.AddToClassList("ns-option-box");
        _teamOptionOverlay.Add(box);

        var title = new Label(LocalizationService.T("ui.code.new_season_controller.re_firma_de_agentes_libres"));
        title.AddToClassList("ns-roster-title");
        box.Add(title);

        var subtitle = new Label(LocalizationService.T("ui.code.new_season_controller.estos_jugadores_declinaron_su_player_opt_2ffbddd3"));
        subtitle.AddToClassList("ns-roster-subtitle");
        box.Add(subtitle);

        var list = new VisualElement();
        list.AddToClassList("ns-option-list");
        box.Add(list);

        var settings = DatabaseManager.Instance.GetLeagueSettings();
        long totalPayroll = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeamId).Sum(p => p.salary);

        foreach (var p in _reSignCandidates)
        {
            long market = DatabaseManager.Instance.EstimateMarketSalary(p.GetCalculatedAverage(), settings != null ? settings.salary_cap : TradeHelper.SALARY_CAP);
            var breakdown = RosterController.GetMaxOfferBreakdown(p, settings, totalPayroll, true);
            long birdMax = breakdown.birdMax > 0 ? breakdown.birdMax : market;

            var row = new VisualElement();
            row.AddToClassList("ns-option-row");

            var info = new Label($"{p.first_name} {p.last_name} ({PositionCodes.GetName(p.position)})"
                + $" — salario previo {FormatMoney(p.salary)}"
                + $" — mercado {FormatMoney(market)}"
                + $" — máximo con Bird rights {FormatMoney(birdMax)}");
            info.AddToClassList("ns-option-info");
            row.Add(info);

            var btns = new VisualElement();
            btns.AddToClassList("ns-option-btns");

            var signBtn = new Button { text = LocalizationService.T("ui.code.new_season_controller.re_firmar") };
            signBtn.AddToClassList("ns-option-btn");
            signBtn.AddToClassList("ns-option-btn--exercise");
            CursorManager.Instance?.RegisterHandCursor(signBtn);

            var releaseBtn = new Button { text = LocalizationService.T("ui.code.new_season_controller.salir_a_mercado") };
            releaseBtn.AddToClassList("ns-option-btn");
            releaseBtn.AddToClassList("ns-option-btn--decline");
            CursorManager.Instance?.RegisterHandCursor(releaseBtn);

            int pid = p.id;
            signBtn.clicked += () => { PlayClick(); SendReSignOffer(pid, market, birdMax); signBtn.SetEnabled(false); releaseBtn.SetEnabled(false); };
            releaseBtn.clicked += () =>
            {
                PlayClick();
                var player = _reSignCandidates.FirstOrDefault(x => x.id == pid);
                if (player != null)
                {
                    player.renewal_cooldown_day = 0;
                    DatabaseManager.Instance.UpdatePlayer(player);
                    AddReSignMessage(player, null);
                }
                signBtn.SetEnabled(false);
                releaseBtn.SetEnabled(false);
            };

            btns.Add(signBtn);
            btns.Add(releaseBtn);
            row.Add(btns);
            list.Add(row);
        }

        var continueBtn = new Button { text = LocalizationService.T("ui.code.new_season_controller.continuar") };
        continueBtn.AddToClassList("ns-roster-continue");
        continueBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _teamOptionOverlay.style.display = DisplayStyle.None;
            CheckRosterAndStart();
        });
        box.Add(continueBtn);
        CursorManager.Instance?.RegisterHandCursor(continueBtn);
    }

    void SendReSignOffer(int playerId, long marketSalary, long birdMax)
    {
        var player = _reSignCandidates.FirstOrDefault(p => p.id == playerId);
        if (player == null) return;

        // Oferta de re-firma: salario = mercado clamp a bird max, años según edad
        long salary = marketSalary < 1_000_000 ? 1_000_000 : marketSalary;
        if (salary > birdMax) salary = birdMax;
        int years = player.age > 35 ? 1 : player.age > 32 ? 2 : player.age > 28 ? 3 : player.age > 25 ? 4 : 5;

        var offer = new OfferData
        {
            manager_id = _manager.id,
            player_id = player.id,
            offer_salary = salary,
            offer_years = years,
            guaranteed_years = years,
            has_team_option = 0,
            has_player_option = 0,
            day_sent = _season?.current_game_day ?? 0,
            offer_type = 0, // renovación / re-firma
            status = "pending",
            processed = 0
        };
        DatabaseManager.Instance.AddOffer(offer);
        AddReSignMessage(player, offer);
    }

    void AddReSignMessage(PlayerData player, OfferData offer)
    {
        string playerName = $"{player.first_name} {player.last_name}";
        int currentDay = _season?.current_game_day ?? 0;
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string body;
        if (offer != null)
            body = $"Has enviado una oferta de re-firma a {playerName}, que declinó su player option.\n" +
                   $"Al ser un agente libre reciente de tu equipo, conservas sus Bird rights.\n\n" +
                   $"Oferta: {offer.offer_salary:N0} $ durante {offer.offer_years} año{(offer.offer_years != 1 ? "s" : "")}.\n" +
                   $"Recibirás su respuesta en el Dashboard en 7 días.";
        else
            body = $"{playerName}, que declinó su player option, sale a la agencia libre.\n" +
                   "Conservas sus Bird rights por si decides re-firmarlo más adelante.";

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = offer != null ? $"Re-firma enviada: {playerName}" : $"Agente libre: {playerName}",
            body = body,
            game_day = currentDay,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = offer != null ? NewsType.REFIRMA_ENVIADA : NewsType.AGENTE_LIBRE
        });
    }

    string FormatMoney(long amount)
    {
        return System.Math.Abs(amount).ToString("N0").Replace(',', '.') + " $";
    }
}
