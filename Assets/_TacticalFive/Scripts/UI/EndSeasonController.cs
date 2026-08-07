using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;

public class EndSeasonController : UIScreenController
{
    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerSeason;
    private Label _headerDate;

    // Content
    private Label _seasonTag;
    private ScrollView _retiringList;
    private ScrollView _fameList;
    private Button _btnDraft;
    private Button _btnNextSeason;
    private Button _btnLottery;
    private VisualElement _lotteryOverlay;
    private VisualElement _famePanel;
    private ScrollView _draftResults;

    private Dictionary<string, Sprite> _logo32;

    protected override void OnEnable()
    {
        base.OnEnable();

        _lotteryOverlay = new VisualElement();
        _lotteryOverlay.AddToClassList("lottery-overlay");
        _root.Add(_lotteryOverlay);

        CursorManager.Instance?.SetDefaultCursor();

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnDraft);
            CursorManager.Instance.RegisterHandCursor(_btnNextSeason);
            CursorManager.Instance.RegisterHandCursor(_btnLottery);
        }
    }

    protected override void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _seasonTag = _root.Q<Label>("SeasonTag");
        _retiringList = _root.Q<ScrollView>("RetiringList");
        _fameList = _root.Q<ScrollView>("FameList");
        _btnDraft = _root.Q<Button>("BtnDraft");
        _btnNextSeason = _root.Q<Button>("BtnNextSeason");
        _btnLottery = _root.Q<Button>("BtnLottery");
        _famePanel = _root.Q<VisualElement>("FamePanel");
        _draftResults = _root.Q<ScrollView>("DraftResults");
    }

    protected override void LoadData()
    {
        base.LoadData();
        if (_season == null) return;

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        _logo32 = new Dictionary<string, Sprite>();
        foreach (var s in logos) _logo32[s.name] = s;

        ProcessAITeamRenewals();

        _btnNextSeason.SetEnabled(false);
    }

    protected override void RegisterCallbacks()
    {
        _btnDraft?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _btnDraft.SetEnabled(false);
            _btnDraft.text = "GENERANDO...";
            var drafted = DraftGenerator.GenerateDraft(_season, _manager.id);
            _btnDraft.text = "DRAFT COMPLETADO";
            if (_famePanel != null) _famePanel.style.display = DisplayStyle.None;
            ShowDraftResults(drafted);
            ShowDraftLotteryModal(drafted);
            _btnNextSeason.SetEnabled(true);
        });

        _btnNextSeason?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.NewSeason); });

        _btnLottery?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowLotteryModal(); });
    }

    protected override void Refresh()
    {
        if (_season == null) return;
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
        string seasonLabel = $"{_season.year_start}-{_season.year_end.ToString().Substring(2)}";
        _seasonTag.text = seasonLabel;

        LoadRetiringPlayers();
        LoadFamePlayers();

        _btnDraft.text = $"EMPEZAR DRAFT {_season.year_end}";
    }

    void LoadRetiringPlayers()
    {
        _retiringList.Clear();
        var players = DatabaseManager.Instance.GetRetiringPlayers();
        if (players.Count == 0)
        {
            var empty = new Label();
            empty.AddToClassList("es-empty");
            empty.text = "No hay jugadores que se retiren esta temporada";
            _retiringList.Add(empty);
            return;
        }
        foreach (var p in players)
        {
            _retiringList.Add(BuildPlayerRow(p));
        }
    }

    void LoadFamePlayers()
    {
        _fameList.Clear();
        var inductees = DatabaseManager.Instance.GetRetiringHallOfFameMembers();
        if (inductees.Count == 0)
        {
            var empty = new Label();
            empty.AddToClassList("es-empty");
            empty.text = "Ningún jugador entra al Salón de la Fama esta temporada";
            _fameList.Add(empty);
            return;
        }
        foreach (var p in inductees)
        {
            _fameList.Add(BuildFameRow(p));
        }
    }

    VisualElement BuildPlayerRow(PlayerData p)
    {
        var row = new VisualElement();
        row.AddToClassList("es-player-row");

        if (p.team_id != 0)
        {
            var logo = new VisualElement();
            logo.AddToClassList("es-mini-logo");
            var team = DatabaseManager.Instance.GetTeamById(p.team_id);
            if (team != null && _logo32.TryGetValue(team.logo, out var sprite))
                logo.style.backgroundImage = new StyleBackground(sprite);
            row.Add(logo);
        }

        var nameLbl = new Label();
        nameLbl.AddToClassList("es-player-name");
        nameLbl.text = $"{p.first_name} {p.last_name}";
        row.Add(nameLbl);

        var posLbl = new Label();
        posLbl.AddToClassList("es-player-pos");
        posLbl.text = PositionCodes.GetShort(p.position);
        row.Add(posLbl);

        var ageLbl = new Label();
        ageLbl.AddToClassList("es-player-age");
        ageLbl.text = $"{p.age} años";
        row.Add(ageLbl);

        return row;
    }

    VisualElement BuildFameRow(PlayerData p)
    {
        var row = new VisualElement();
        row.AddToClassList("es-player-row");
        row.AddToClassList("es-player-fame-row");

        if (p.team_id != 0)
        {
            var logo = new VisualElement();
            logo.AddToClassList("es-mini-logo");
            var team = DatabaseManager.Instance.GetTeamById(p.team_id);
            if (team != null && _logo32.TryGetValue(team.logo, out var sprite))
                logo.style.backgroundImage = new StyleBackground(sprite);
            row.Add(logo);
        }

        var nameLbl = new Label();
        nameLbl.AddToClassList("es-player-name");
        nameLbl.text = $"{p.first_name} {p.last_name}";
        row.Add(nameLbl);

        var credentialsLbl = new Label();
        credentialsLbl.AddToClassList("es-player-credentials");
        credentialsLbl.text = p.rings > 0 || p.finals_mvps > 0
            ? $"{p.rings} anillo(s)  ·  {p.finals_mvps} Finales MVP"
            : "";
        row.Add(credentialsLbl);

        var fameLbl = new Label();
        fameLbl.AddToClassList("es-player-fame");
        fameLbl.text = "Salón de la Fama";
        row.Add(fameLbl);

        return row;
    }

    void ProcessAITeamRenewals()
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        foreach (var team in allTeams)
        {
            if (team.id == _myTeam.id) continue;

            var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            var candidates = roster
                .Where(p => p.contract_years == 1 && p.age < 40 && p.GetCalculatedAverage() >= 80)
                .ToList();

            foreach (var player in candidates)
            {
                int years = CalcRenewYears(player.age);
                long newSalary = CalcRenewSalary(player.salary, player.age);
                player.contract_years = years;
                player.guaranteed_years = years;
                player.salary = newSalary;
                DatabaseManager.Instance.UpdatePlayer(player);
            }
        }
    }

    int CalcRenewYears(int age)
    {
        if (age <= 25) return 5;
        if (age <= 28) return 4;
        if (age <= 32) return 3;
        if (age < 40) return 2;
        return 1;
    }

    long CalcRenewSalary(long currentSalary, int age)
    {
        double multiplier;
        if (age <= 25) multiplier = 1.20;
        else if (age <= 30) multiplier = 1.10;
        else multiplier = 1.05;

        long newSalary = (long)(currentSalary * multiplier);
        newSalary = (long)(Math.Round(newSalary / 100000.0) * 100000);
        if (newSalary < currentSalary) newSalary = currentSalary;
        return newSalary;
    }

    void ShowDraftResults(List<DraftGenerator.DraftPickResult> drafted)
    {
        _draftResults.contentContainer.style.flexDirection = FlexDirection.Column;
        _draftResults.Clear();
        foreach (var r in drafted)
        {
            _draftResults.Add(BuildDraftPick(r));
        }
    }

    VisualElement BuildDraftPick(DraftGenerator.DraftPickResult r)
    {
        var pick = new VisualElement();
        pick.AddToClassList("draft-pick");

        var header = new VisualElement();
        header.AddToClassList("draft-pick-header");

        var numLbl = new Label();
        numLbl.AddToClassList("draft-pick-num");
        string roundPrefix = r.PickNumber <= 30 ? "R1" : "R2";
        numLbl.text = $"{roundPrefix} #{r.PickNumber}";
        header.Add(numLbl);

        var team = r.Team;
        var teamRow = new VisualElement();
        teamRow.AddToClassList("draft-team-row");

        var teamLogo = new VisualElement();
        teamLogo.AddToClassList("draft-team-logo");
        if (team != null && _logo32.TryGetValue(team.logo, out var sprite))
            teamLogo.style.backgroundImage = new StyleBackground(sprite);
        teamRow.Add(teamLogo);

        var teamName = new Label();
        teamName.AddToClassList("draft-team-name");
        teamName.text = team?.name ?? "";
        teamRow.Add(teamName);

        header.Add(teamRow);
        pick.Add(header);

        var p = r.Player;
        var info = new VisualElement();
        info.AddToClassList("draft-player-info");

        var nameLbl = new Label();
        nameLbl.AddToClassList("draft-player-name");
        nameLbl.text = $"{p.first_name} {p.last_name}";
        info.Add(nameLbl);

        var posLbl = new Label();
        posLbl.AddToClassList("draft-player-pos");
        posLbl.text = PositionCodes.GetShort(p.position);
        info.Add(posLbl);

        if (!string.IsNullOrEmpty(p.secondary_position))
        {
            var secLbl = new Label();
            secLbl.AddToClassList("draft-player-sec-pos");
            secLbl.text = PositionCodes.GetShort(p.secondary_position);
            info.Add(secLbl);
        }

        var detailLbl = new Label();
        detailLbl.AddToClassList("draft-player-detail");
        string college = !string.IsNullOrEmpty(p.college) ? $" · {p.college}" : "";
        detailLbl.text = $"{p.age} años · {p.height_cm}cm · {p.weight_kg}kg · {CountryCodes.GetName(p.nationality)}{college}";
        info.Add(detailLbl);

        pick.Add(info);

        var attrs = new VisualElement();
        attrs.AddToClassList("draft-player-attrs");

        attrs.Add(BuildMedAttr(p.GetCalculatedAverage()));
        attrs.Add(MakeAttr("VEL", p.speed));
        attrs.Add(MakeAttr("TIR", p.shooting));
        attrs.Add(MakeAttr("3PT", p.three_point));
        attrs.Add(MakeAttr("PAS", p.passing));
        attrs.Add(MakeAttr("MAN", p.dribbling));
        attrs.Add(MakeAttr("DEF", p.defense));
        attrs.Add(MakeAttr("REB", p.rebounding));
        attrs.Add(MakeAttr("ATL", p.athleticism));
        attrs.Add(MakeAttr("IQ", p.iq));
        attrs.Add(MakeAttr("ROB", p.steals));
        attrs.Add(MakeAttr("TAP", p.blocks));
        attrs.Add(MakeAttr("MOR", p.morale));

        pick.Add(attrs);
        return pick;
    }

    VisualElement MakeAttr(string label, int value)
    {
        var box = new VisualElement();
        box.AddToClassList("draft-attr");

        var lbl = new Label();
        lbl.AddToClassList("draft-attr-lbl");
        lbl.text = label;
        box.Add(lbl);

        var val = new Label();
        val.AddToClassList("draft-attr-val");
        val.text = value.ToString();
        box.Add(val);

        return box;
    }

    VisualElement BuildMedAttr(int value)
    {
        var box = new VisualElement();
        box.AddToClassList("draft-attr");
        box.AddToClassList("draft-attr-med");

        var lbl = new Label();
        lbl.AddToClassList("draft-attr-lbl");
        lbl.text = "MED";
        box.Add(lbl);

        var val = new Label();
        val.AddToClassList("draft-attr-val");
        val.text = value.ToString();
        box.Add(val);

        if (value > 84)
            box.AddToClassList("draft-attr-med--high");
        else if (value >= 70)
            box.AddToClassList("draft-attr-med--mid");
        else
            box.AddToClassList("draft-attr-med--low");

        return box;
    }

    void ShowDraftLotteryModal(List<DraftGenerator.DraftPickResult> drafted)
    {
        if (drafted == null || drafted.Count < 14) return;

        _lotteryOverlay.Clear();
        _lotteryOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("lottery-box");
        _lotteryOverlay.Add(box);

        var title = new Label($"DRAFT {_season.year_end} — PRIMERA RONDA");
        title.AddToClassList("lottery-title");
        box.Add(title);

        var subtitle = new Label("Resultado del sorteo de la lotería");
        subtitle.AddToClassList("lottery-subtitle");
        box.Add(subtitle);

        var grid = new VisualElement();
        grid.AddToClassList("lottery-grid");
        box.Add(grid);

        var col1 = new VisualElement();
        col1.AddToClassList("lottery-column");
        var col2 = new VisualElement();
        col2.AddToClassList("lottery-column");
        grid.Add(col1);
        grid.Add(col2);

        for (int i = 0; i < 14 && i < drafted.Count; i++)
        {
            var r = drafted[i];
            var target = i < 7 ? col1 : col2;
            target.Add(BuildDraftLotteryRow(r));
        }

        var closeBtn = new Button();
        closeBtn.text = "CERRAR";
        closeBtn.AddToClassList("lottery-close-btn");
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _lotteryOverlay.style.display = DisplayStyle.None;
        });
        box.Add(closeBtn);
        CursorManager.Instance?.RegisterHandCursor(closeBtn);
    }

    VisualElement BuildDraftLotteryRow(DraftGenerator.DraftPickResult r)
    {
        var row = new VisualElement();
        row.AddToClassList("lottery-row");
        row.AddToClassList("lottery-draft-row");

        var pickLabel = $"{(r.PickNumber == 1 ? "1st" : r.PickNumber == 2 ? "2nd" : r.PickNumber == 3 ? "3rd" : r.PickNumber + "th")}";

        var rankLbl = new Label($"#{pickLabel}");
        rankLbl.AddToClassList("lottery-rank");
        rankLbl.style.minWidth = 40;
        row.Add(rankLbl);

        var logo = new VisualElement();
        logo.AddToClassList("lottery-logo");
        if (_logo32.TryGetValue(r.Team.logo, out var sprite))
            logo.style.backgroundImage = new StyleBackground(sprite);
        row.Add(logo);

        var infoCol = new VisualElement();
        infoCol.style.flexDirection = FlexDirection.Column;
        infoCol.style.flexGrow = 1;
        row.Add(infoCol);

        var teamNameLbl = new Label($"Con la {pickLabel} elección,  {r.Team.name}  ha seleccionado a:");
        teamNameLbl.AddToClassList("lottery-draft-team");
        infoCol.Add(teamNameLbl);

        var p = r.Player;
        string collegeStr = !string.IsNullOrEmpty(p.college) ? $" · {p.college}" : "";
        var playerLbl = new Label($"{p.first_name} {p.last_name}  ·  {PositionCodes.GetShort(p.position)}  ·  MED: {p.overall}{collegeStr}");
        playerLbl.AddToClassList("lottery-draft-player");
        infoCol.Add(playerLbl);

        return row;
    }

    void ShowLotteryModal()
    {
        _lotteryOverlay.Clear();
        _lotteryOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("lottery-box");
        _lotteryOverlay.Add(box);

        var title = new Label("LOTERÍA DEL DRAFT");
        title.AddToClassList("lottery-title");
        box.Add(title);

        var subtitle = new Label($"Probabilidades para el Draft {_season.year_end}");
        subtitle.AddToClassList("lottery-subtitle");
        box.Add(subtitle);

        var teams = BuildLotteryOrder();
        var grid = new VisualElement();
        grid.AddToClassList("lottery-grid");
        box.Add(grid);

        var col1 = new VisualElement();
        col1.AddToClassList("lottery-column");
        var col2 = new VisualElement();
        col2.AddToClassList("lottery-column");
        grid.Add(col1);
        grid.Add(col2);

        for (int i = 0; i < teams.Count; i++)
        {
            var target = i < 7 ? col1 : col2;
            target.Add(BuildLotteryRow(i + 1, teams[i].team, teams[i].pct));
        }

        var closeBtn = new Button();
        closeBtn.text = "CERRAR";
        closeBtn.AddToClassList("lottery-close-btn");
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _lotteryOverlay.style.display = DisplayStyle.None;
        });
        box.Add(closeBtn);
        CursorManager.Instance?.RegisterHandCursor(closeBtn);
    }

    VisualElement BuildLotteryRow(int rank, TeamData team, string pct)
    {
        var row = new VisualElement();
        row.AddToClassList("lottery-row");

        var rankLbl = new Label($"#{rank}");
        rankLbl.AddToClassList("lottery-rank");
        row.Add(rankLbl);

        var logo = new VisualElement();
        logo.AddToClassList("lottery-logo");
        if (_logo32.TryGetValue(team.logo, out var sprite))
            logo.style.backgroundImage = new StyleBackground(sprite);
        row.Add(logo);

        var nameLbl = new Label(team.name);
        nameLbl.AddToClassList("lottery-name");
        row.Add(nameLbl);

        var pctLbl = new Label(pct);
        pctLbl.AddToClassList("lottery-pct");
        row.Add(pctLbl);

        return row;
    }

    List<(TeamData team, string pct)> BuildLotteryOrder()
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);

        var standings = allTeams.Select(t =>
        {
            var teamGames = allGames.Where(g =>
                (g.home_team_id == t.id || g.away_team_id == t.id) && g.is_played == 1).ToList();
            int wins = teamGames.Count(g =>
                (g.home_team_id == t.id && g.home_score > g.away_score) ||
                (g.away_team_id == t.id && g.away_score > g.home_score));
            int total = teamGames.Count;
            float winPct = total > 0 ? (float)wins / total : 0f;
            return new { Team = t, Wins = wins, Total = total, WinPct = winPct };
        })
        .OrderBy(s => s.WinPct)
        .ThenBy(s => s.Wins)
        .Take(14)
        .ToList();

        double[] odds = {
            0.140, 0.140, 0.140, 0.125, 0.105,
            0.090, 0.075, 0.060, 0.045, 0.030,
            0.020, 0.015, 0.010, 0.005
        };

        var result = new List<(TeamData, string)>();
        for (int i = 0; i < standings.Count && i < odds.Length; i++)
        {
            result.Add((standings[i].Team, $"{odds[i] * 100:F1}%"));
        }
        return result;
    }
}
