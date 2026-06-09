using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;
using System.Linq;

public class PlayerAwardsController : MonoBehaviour
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
    private VisualElement _mvpPlayerLogo;
    private Label _mvpPlayerName;
    private Label _mvpPts;
    private Label _mvpReb;
    private Label _mvpAst;
    private VisualElement _rookiePlayerLogo;
    private Label _rookiePlayerName;
    private Label _rookiePts;
    private Label _rookieReb;
    private Label _rookieAst;
    private VisualElement _quintetGrid;
    private VisualElement _rookieGrid;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;

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

        var btnEnd = _root.Q<Button>("BtnEndSeason");
        if (btnEnd != null)
            btnEnd.RegisterCallback<ClickEvent>(_ => ScreenManager.Instance.GoTo(GameScreen.MainMenu));
    }

    void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _seasonTag = _root.Q<Label>("SeasonTag");
        _mvpPlayerLogo = _root.Q<VisualElement>("MvpPlayerLogo");
        _mvpPlayerName = _root.Q<Label>("MvpPlayerName");
        _mvpPts = _root.Q<Label>("MvpPts");
        _mvpReb = _root.Q<Label>("MvpReb");
        _mvpAst = _root.Q<Label>("MvpAst");
        _rookiePlayerLogo = _root.Q<VisualElement>("RookiePlayerLogo");
        _rookiePlayerName = _root.Q<Label>("RookiePlayerName");
        _rookiePts = _root.Q<Label>("RookiePts");
        _rookieReb = _root.Q<Label>("RookieReb");
        _rookieAst = _root.Q<Label>("RookieAst");
        _quintetGrid = _root.Q<VisualElement>("QuintetGrid");
        _rookieGrid = _root.Q<VisualElement>("RookieGrid");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        if (_myTeam == null) return;
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        if (_season == null) return;

        RefreshHeader();
        RefreshContent();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos) logoDict[s.name] = s;
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

        var logo32 = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        var logo32Dict = new Dictionary<string, Sprite>();
        foreach (var s in logo32) logo32Dict[s.name] = s;

        int seasonId = _season.id;
        int managerId = _manager.id;

        var mvp = DatabaseManager.Instance.GetRegularSeasonMVP(seasonId, managerId);
        if (mvp != null)
        {
            _mvpPlayerName.text = mvp.PlayerName;
            if (logo32Dict.TryGetValue(mvp.TeamKeyword, out var mvpSprite))
                _mvpPlayerLogo.style.backgroundImage = new StyleBackground(mvpSprite);
            _mvpPts.text = mvp.AvgPts.ToString("F1");
            _mvpReb.text = mvp.AvgReb.ToString("F1");
            _mvpAst.text = mvp.AvgAst.ToString("F1");
        }

        var rookie = DatabaseManager.Instance.GetRookieOfYear(seasonId, managerId);
        if (rookie != null)
        {
            _rookiePlayerName.text = rookie.PlayerName;
            if (logo32Dict.TryGetValue(rookie.TeamKeyword, out var rookieSprite))
                _rookiePlayerLogo.style.backgroundImage = new StyleBackground(rookieSprite);
            _rookiePts.text = rookie.AvgPts.ToString("F1");
            _rookieReb.text = rookie.AvgReb.ToString("F1");
            _rookieAst.text = rookie.AvgAst.ToString("F1");
        }

        var allStar = DatabaseManager.Instance.GetAllStarTeam(seasonId, managerId);
        foreach (var p in allStar)
            _quintetGrid.Add(BuildQuintetCard(p, true, logo32Dict));

        var allRookie = DatabaseManager.Instance.GetAllRookieTeam(seasonId, managerId);
        foreach (var p in allRookie)
            _rookieGrid.Add(BuildQuintetCard(p, false, logo32Dict));
    }

    VisualElement BuildQuintetCard(PlayerAwardInfo p, bool isFive, Dictionary<string, Sprite> logo32)
    {
        var card = new VisualElement();
        card.AddToClassList("quintet-card");
        card.AddToClassList(isFive ? "quintet-card--five" : "quintet-card--rookie");

        var posLbl = new Label();
        posLbl.AddToClassList("quintet-card-pos");
        posLbl.text = p.Position;
        card.Add(posLbl);

        var logo = new VisualElement();
        logo.AddToClassList("quintet-card-logo");
        if (logo32.TryGetValue(p.TeamKeyword, out var sprite))
            logo.style.backgroundImage = new StyleBackground(sprite);
        card.Add(logo);

        var nameLbl = new Label();
        nameLbl.AddToClassList("quintet-card-name");
        nameLbl.text = p.PlayerName;
        card.Add(nameLbl);

        var statRow = new VisualElement();
        statRow.AddToClassList("quintet-stat-row");

        statRow.Add(MakeSmallStatBox(p.AvgPts.ToString("F1"), "PTS"));
        statRow.Add(MakeSmallStatBox(p.AvgReb.ToString("F1"), "REB"));
        statRow.Add(MakeSmallStatBox(p.AvgAst.ToString("F1"), "AST"));

        card.Add(statRow);
        return card;
    }

    VisualElement MakeSmallStatBox(string val, string label)
    {
        var box = new VisualElement();
        box.AddToClassList("quintet-stat-box");

        var valLbl = new Label();
        valLbl.AddToClassList("quintet-stat-val");
        valLbl.text = val;
        box.Add(valLbl);

        var lblLbl = new Label();
        lblLbl.AddToClassList("quintet-stat-lbl");
        lblLbl.text = label;
        box.Add(lblLbl);

        return box;
    }
}
