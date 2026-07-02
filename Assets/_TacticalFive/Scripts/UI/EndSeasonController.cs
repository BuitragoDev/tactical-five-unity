using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;

public class EndSeasonController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerSeason;
    private Label _headerDate;

    // Content
    private Label _seasonTag;
    private ScrollView _retiringList;
    private ScrollView _expiringList;
    private Button _btnDraft;
    private Button _btnNextSeason;
    private Button _btnRenewAll;
    private Button _btnLottery;
    private VisualElement _lotteryOverlay;
    private VisualElement _expiringPanel;
    private ScrollView _draftResults;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;

    private Dictionary<string, Sprite> _logo32;

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        _lotteryOverlay = new VisualElement();
        _lotteryOverlay.AddToClassList("lottery-overlay");
        _root.Add(_lotteryOverlay);

        CacheReferences();
        LoadData();
    }

    void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _seasonTag = _root.Q<Label>("SeasonTag");
        _retiringList = _root.Q<ScrollView>("RetiringList");
        _expiringList = _root.Q<ScrollView>("ExpiringList");
        _btnDraft = _root.Q<Button>("BtnDraft");
        _btnNextSeason = _root.Q<Button>("BtnNextSeason");
        _btnRenewAll = _root.Q<Button>("BtnRenewAll");
        _btnLottery = _root.Q<Button>("BtnLottery");
        _expiringPanel = _root.Q<VisualElement>("ExpiringPanel");
        _draftResults = _root.Q<ScrollView>("DraftResults");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        if (_myTeam == null) return;
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        if (_season == null) return;

        CollectLuxuryTax();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        _logo32 = new Dictionary<string, Sprite>();
        foreach (var s in logos) _logo32[s.name] = s;

        RefreshHeader();
        RefreshContent();

        _btnNextSeason.SetEnabled(false);

        _btnDraft.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _btnDraft.SetEnabled(false);
            _btnDraft.text = "GENERANDO...";
            var drafted = DraftGenerator.GenerateDraft(_season, _manager.id);
            _btnDraft.text = "DRAFT COMPLETADO";
            _btnRenewAll.SetEnabled(false);
            if (_expiringPanel != null) _expiringPanel.style.display = DisplayStyle.None;
            ShowDraftResults(drafted);
            ShowDraftLotteryModal(drafted);
            _btnNextSeason.SetEnabled(true);
        });

        _btnNextSeason.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.NewSeason); });

        _btnRenewAll.RegisterCallback<ClickEvent>(_ => { PlayClick(); RenewAll(); });

        _btnLottery.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowLotteryModal(); });
    }

    void RefreshHeader()
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
        LoadExpiringPlayers();

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
            _retiringList.Add(BuildPlayerRow(p, false));
        }
    }

    void LoadExpiringPlayers()
    {
        _expiringList.Clear();
        var players = DatabaseManager.Instance.GetExpiringPlayers();
        if (players.Count == 0)
        {
            var empty = new Label();
            empty.AddToClassList("es-empty");
            empty.text = "No hay contratos expirando";
            _expiringList.Add(empty);
            return;
        }
        foreach (var p in players)
        {
            _expiringList.Add(BuildPlayerRow(p, true));
        }
    }

    VisualElement BuildPlayerRow(PlayerData p, bool isExpiring)
    {
        var row = new VisualElement();
        row.AddToClassList("es-player-row");
        if (isExpiring) row.AddToClassList("es-player-expiry-row");

        var logo = new VisualElement();
        logo.AddToClassList("es-mini-logo");
        var team = DatabaseManager.Instance.GetTeamById(p.team_id);
        if (team != null && _logo32.TryGetValue(team.logo, out var sprite))
            logo.style.backgroundImage = new StyleBackground(sprite);
        row.Add(logo);

        var nameLbl = new Label();
        nameLbl.AddToClassList("es-player-name");
        nameLbl.text = $"{p.first_name} {p.last_name}";
        row.Add(nameLbl);

        var posLbl = new Label();
        posLbl.AddToClassList("es-player-pos");
        posLbl.text = p.position;
        row.Add(posLbl);

        if (isExpiring)
        {
            var salaryLbl = new Label();
            salaryLbl.AddToClassList("es-player-salary");
            salaryLbl.text = $"${p.salary:N0}";
            row.Add(salaryLbl);

            var renewBtn = new Button();
            renewBtn.AddToClassList("btn-renew");
            renewBtn.text = "Renovar";
            int capturedId = p.id;
            renewBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                RenewPlayer(capturedId);
                row.RemoveFromHierarchy();
            });
            row.Add(renewBtn);
        }
        else
        {
            var ageLbl = new Label();
            ageLbl.AddToClassList("es-player-age");
            ageLbl.text = $"{p.age} años";
            row.Add(ageLbl);
        }

        return row;
    }

    void RenewPlayer(int playerId)
    {
        var player = DatabaseManager.Instance.GetPlayerById(playerId);
        if (player == null) return;

        int years = CalcRenewYears(player.age);
        long newSalary = CalcRenewSalary(player.salary, player.age);

        player.contract_years = years;
        player.salary = newSalary;
        DatabaseManager.Instance.UpdatePlayer(player);
    }

    void RenewAll()
    {
        var players = DatabaseManager.Instance.GetExpiringPlayers();
        foreach (var p in players)
        {
            if (p.age >= 40) continue;
            int years = CalcRenewYears(p.age);
            long newSalary = CalcRenewSalary(p.salary, p.age);
            p.contract_years = years;
            p.salary = newSalary;
            DatabaseManager.Instance.UpdatePlayer(p);
        }
        LoadExpiringPlayers();
        _btnRenewAll.SetEnabled(false);
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
        numLbl.text = $"#{r.PickNumber}";
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
        posLbl.text = p.position;
        info.Add(posLbl);

        var detailLbl = new Label();
        detailLbl.AddToClassList("draft-player-detail");
        detailLbl.text = $"{p.age} años · {p.height_cm}cm · {p.weight_kg}kg · {p.nationality}";
        info.Add(detailLbl);

        var ovrLbl = new Label();
        ovrLbl.AddToClassList("draft-player-ovr");
        ovrLbl.text = $"OVR: {p.overall}";
        info.Add(ovrLbl);

        pick.Add(info);

        var attrs = new VisualElement();
        attrs.AddToClassList("draft-player-attrs");

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

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
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
        var playerLbl = new Label($"{p.first_name} {p.last_name}  ·  {p.position}  ·  OVR: {p.overall}");
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

    void CollectLuxuryTax()
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        string nowStr = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        int taxedCount = 0;
        long totalTax = 0;

        foreach (var team in allTeams)
        {
            var players = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            long payroll = players.Sum(p => p.salary);
            long tax = TradeHelper.CalculateLuxuryTax(payroll);
            if (tax > 0)
            {
                taxedCount++;
                totalTax += tax;
                DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
                {
                    team_id = team.id,
                    season_id = _season.id,
                    record_type = FinanceRecord.TYPE_TAX,
                    game_day = _season.current_game_day,
                    amount = -tax,
                    created_at = nowStr
                });
                Debug.Log($"[LuxuryTax] {team.name}: payroll ${payroll:N0}, tax ${tax:N0}");
            }
        }

        if (taxedCount > 0)
            Debug.Log($"[LuxuryTax] Total: {taxedCount} teams taxed, ${totalTax:N0} collected.");
        else
            Debug.Log("[LuxuryTax] No teams over the luxury tax threshold.");
    }
}
