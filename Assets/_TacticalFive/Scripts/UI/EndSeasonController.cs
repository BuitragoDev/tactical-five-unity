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

        _btnNextSeason.RegisterCallback<ClickEvent>(_ => ScreenManager.Instance.GoTo(GameScreen.MainMenu));

        _btnDraft.RegisterCallback<ClickEvent>(_ =>
        {
            _btnDraft.SetEnabled(false);
            _btnDraft.text = "GENERANDO...";
        });

        _btnRenewAll.RegisterCallback<ClickEvent>(_ => Debug.Log("Renew all clicked"));
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
        var players = DatabaseManager.Instance.GetRetiringPlayers(_myTeam.id);
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
            renewBtn.RegisterCallback<ClickEvent>(_ => Debug.Log($"Renew {p.first_name} {p.last_name}"));
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
}
