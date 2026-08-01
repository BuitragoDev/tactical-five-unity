using UnityEngine;
using UnityEngine.UIElements;
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

    protected override void OnEnable()
    {
        base.OnEnable();

        _rosterOverlay = new VisualElement();
        _rosterOverlay.AddToClassList("ns-roster-overlay");
        _root.Add(_rosterOverlay);

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

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
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

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos64) logoDict[s.name] = s;
        if (logoDict.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
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
            _noteText.text = "Selecciona tu equipo actual o uno de los 3 equipos disponibles para la nueva temporada.";
        else
            _noteText.text = "Continuarás con tu equipo actual en la nueva temporada.";
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
                badge.text = "SELECCIONADO";
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
        var roster = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeamId);
        if (roster.Count > TradeHelper.MAX_ROSTER)
        {
            OpenRosterModal(roster);
            return;
        }
        ExecuteStartSeason();
    }

    void ExecuteStartSeason()
    {
        _btnStartSeason.SetEnabled(false);
        _btnStartSeason.text = "INICIANDO...";

        int newTeamId = _selectedTeamId;
        int oldTeamId = _manager.team_id; // Capture before possible update

        // Update manager team if changed
        if (_manager.team_id != newTeamId)
        {
            _manager.team_id = newTeamId;
            DatabaseManager.Instance.SaveManager(_manager);
        }

        // Execute the full new season logic
        DatabaseManager.Instance.StartNewSeason(
            _season.id,
            newTeamId,
            _season.game_mode,
            _manager.id,
            oldTeamId
        );

        // Navigate to Preseason to set up friendly games
        ScreenManager.Instance.GoTo(GameScreen.Preseason);
    }

    // ── ROSTER TRIM MODAL ────────────────────────────────

    void OpenRosterModal(List<PlayerData> roster)
    {
        _rosterPlayers = roster;
        _rosterActiveCount = roster.Count;

        _rosterOverlay.Clear();
        _rosterOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("ns-roster-box");
        _rosterOverlay.Add(box);

        var title = new Label("PLANTILLA COMPLETA");
        title.AddToClassList("ns-roster-title");
        box.Add(title);

        var subtitle = new Label($"Tu plantilla tiene {_rosterActiveCount} jugadores. El máximo es {TradeHelper.MAX_ROSTER}.\nDebes rescindir contratos hasta alcanzar el límite.");
        subtitle.AddToClassList("ns-roster-subtitle");
        box.Add(subtitle);

        _rosterCountLabel = new Label($"Jugadores: {_rosterActiveCount} / {TradeHelper.MAX_ROSTER}");
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

        int perCol = (int)System.Math.Ceiling(roster.Count / 4f);
        for (int i = 0; i < roster.Count; i++)
        {
            VisualElement target;
            if (i < perCol) target = col1;
            else if (i < perCol * 2) target = col2;
            else if (i < perCol * 3) target = col3;
            else target = col4;
            target.Add(BuildPlayerCard(roster[i]));
        }

        _btnRosterContinue = new Button();
        _btnRosterContinue.AddToClassList("ns-roster-continue");
        _btnRosterContinue.text = "CONTINUAR A LA SIGUIENTE TEMPORADA";
        _btnRosterContinue.SetEnabled(_rosterActiveCount <= TradeHelper.MAX_ROSTER);
        _btnRosterContinue.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _rosterOverlay.style.display = DisplayStyle.None;
            ExecuteStartSeason();
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
        medLbl.text = "MED";
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
        btn.text = "RESCINDIR CONTRATO";
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
            sender_type = 0,
            sender_id = 0,
            title = "Jugador despedido",
            body = $"El club ha decidido rescindir el contrato de {playerName} con efecto inmediato.\n\n" +
                   $"La operación supone una penalización económica de {penalty:N0} €, que ha sido cargada a las cuentas del club.\n\n" +
                   $"La salida del jugador libera una plaza en la plantilla y su salario dejará de computar a partir de esta fecha.\n\n" +
                   $"Coste de rescisión: {penalty:N0} €\n" +
                   $"Ahorro salarial restante: {remainingSalary:N0} €\n" +
                   $"Balance neto de la operación: {netBalance:N0} €",
            game_day = currentDay,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0
        });

        btn.text = "DESPEDIDO";
        btn.SetEnabled(false);
        btn.AddToClassList("ns-roster-btn--dismissed");

        _rosterActiveCount = _rosterPlayers.Count(p => p.team_id == _selectedTeamId);
        _rosterCountLabel.text = $"Jugadores: {_rosterActiveCount} / {TradeHelper.MAX_ROSTER}";

        if (_rosterActiveCount <= TradeHelper.MAX_ROSTER)
        {
            _btnRosterContinue.SetEnabled(true);
            CursorManager.Instance?.RegisterHandCursor(_btnRosterContinue);
        }
    }
}
