using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
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

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        _logo32 = new Dictionary<string, Sprite>();
        foreach (var s in logos) _logo32[s.name] = s;

        RefreshHeader();
        RefreshContent();

        _btnNextSeason.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.MainMenu); });

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
        });

        _btnRenewAll.RegisterCallback<ClickEvent>(_ => { PlayClick(); RenewAll(); });
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

    void ShowDraftResults(List<PlayerData> drafted)
    {
        _draftResults.contentContainer.style.flexDirection = FlexDirection.Column;
        _draftResults.Clear();
        for (int i = 0; i < drafted.Count; i++)
        {
            var p = drafted[i];
            _draftResults.Add(BuildDraftPick(i + 1, p));
        }
    }

    VisualElement BuildDraftPick(int pickNum, PlayerData p)
    {
        var pick = new VisualElement();
        pick.AddToClassList("draft-pick");

        var header = new VisualElement();
        header.AddToClassList("draft-pick-header");

        var numLbl = new Label();
        numLbl.AddToClassList("draft-pick-num");
        numLbl.text = $"#{pickNum}";
        header.Add(numLbl);

        var team = DatabaseManager.Instance.GetTeamById(p.team_id);
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
}
