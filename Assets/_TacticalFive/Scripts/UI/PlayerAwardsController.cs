using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;
using System.Linq;

public class PlayerAwardsController : UIScreenController
{
    private static readonly Dictionary<string, string> _posSpanish = new()
    {
        { "PG", "Base" },
        { "SG", "Escolta" },
        { "SF", "Alero" },
        { "PF", "Ala-Pívot" },
        { "C",  "Pívot" },
    };

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerSeason;
    private Label _headerDate;

    // Content
    private Label _seasonTag;
    private VisualElement _mvpPlayerPhoto;
    private Label _mvpPlayerName;
    private Label _mvpPlayerTeam;
    private Label _mvpPts;
    private Label _mvpReb;
    private Label _mvpAst;
    private VisualElement _rookiePlayerPhoto;
    private Label _rookiePlayerName;
    private Label _rookiePlayerTeam;
    private Label _rookiePts;
    private Label _rookieReb;
    private Label _rookieAst;
    private VisualElement _quintetGrid;
    private VisualElement _rookieGrid;

    protected override void OnEnable()
    {
        base.OnEnable();
        CursorManager.Instance?.SetDefaultCursor();
    }

    protected override void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _seasonTag = _root.Q<Label>("SeasonTag");
        _mvpPlayerPhoto = _root.Q<VisualElement>("MvpPlayerPhoto");
        _mvpPlayerName = _root.Q<Label>("MvpPlayerName");
        _mvpPlayerTeam = _root.Q<Label>("MvpPlayerTeam");
        _mvpPts = _root.Q<Label>("MvpPts");
        _mvpReb = _root.Q<Label>("MvpReb");
        _mvpAst = _root.Q<Label>("MvpAst");
        _rookiePlayerPhoto = _root.Q<VisualElement>("RookiePlayerPhoto");
        _rookiePlayerName = _root.Q<Label>("RookiePlayerName");
        _rookiePlayerTeam = _root.Q<Label>("RookiePlayerTeam");
        _rookiePts = _root.Q<Label>("RookiePts");
        _rookieReb = _root.Q<Label>("RookieReb");
        _rookieAst = _root.Q<Label>("RookieAst");
        _quintetGrid = _root.Q<VisualElement>("QuintetGrid");
        _rookieGrid = _root.Q<VisualElement>("RookieGrid");
    }

    protected override void LoadData()
    {
        base.LoadData();
    }

    protected override void RegisterCallbacks()
    {
        var btnEnd = _root.Q<Button>("BtnEndSeason");
        if (btnEnd != null)
        {
            btnEnd.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.EndSeason); });
            CursorManager.Instance?.RegisterHandCursor(btnEnd);
        }
    }

    protected override void Refresh()
    {
        if (_season == null || _manager == null) return;
        RefreshHeader();
        RefreshContent();
    }

    protected override void RefreshHeader()
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

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var abbrevByKeyword = new Dictionary<string, string>();
        foreach (var t in allTeams)
            if (!string.IsNullOrEmpty(t.logo))
                abbrevByKeyword[t.logo] = t.abbreviation;

        int seasonId = _season.id;
        int managerId = _manager.id;

        var mvp = DatabaseManager.Instance.GetRegularSeasonMVP(seasonId, managerId);
        if (mvp != null)
        {
            string mvpAbbrev = abbrevByKeyword.TryGetValue(mvp.TeamKeyword, out var ma) ? ma : "";
            _mvpPlayerName.text = string.IsNullOrEmpty(mvpAbbrev) ? mvp.PlayerName : $"{mvp.PlayerName} ({mvpAbbrev})";
            _mvpPlayerTeam.style.display = DisplayStyle.None;
            Texture2D mvpTex = PlayerPhotoHelper.Load(mvp.PlayerId, mvp.Photo);
            _mvpPlayerPhoto.style.backgroundImage = new StyleBackground(mvpTex);
            _mvpPts.text = mvp.AvgPts.ToString("F1");
            _mvpReb.text = mvp.AvgReb.ToString("F1");
            _mvpAst.text = mvp.AvgAst.ToString("F1");
        }

        var rookie = DatabaseManager.Instance.GetRookieOfYear(seasonId, managerId);
        if (rookie != null)
        {
            string rookieAbbrev = abbrevByKeyword.TryGetValue(rookie.TeamKeyword, out var ra) ? ra : "";
            _rookiePlayerName.text = string.IsNullOrEmpty(rookieAbbrev) ? rookie.PlayerName : $"{rookie.PlayerName} ({rookieAbbrev})";
            _rookiePlayerTeam.style.display = DisplayStyle.None;
            Texture2D rookieTex = PlayerPhotoHelper.Load(rookie.PlayerId, rookie.Photo);
            _rookiePlayerPhoto.style.backgroundImage = new StyleBackground(rookieTex);
            _rookiePts.text = rookie.AvgPts.ToString("F1");
            _rookieReb.text = rookie.AvgReb.ToString("F1");
            _rookieAst.text = rookie.AvgAst.ToString("F1");
        }

        var allStar = DatabaseManager.Instance.GetAllStarTeam(seasonId, managerId);
        foreach (var p in allStar)
            _quintetGrid.Add(BuildQuintetCard(p, true, abbrevByKeyword));

        var allRookie = DatabaseManager.Instance.GetAllRookieTeam(seasonId, managerId);
        foreach (var p in allRookie)
            _rookieGrid.Add(BuildQuintetCard(p, false, abbrevByKeyword));
    }

    VisualElement BuildQuintetCard(PlayerAwardInfo p, bool isFive, Dictionary<string, string> abbrevByKeyword)
    {
        var card = new VisualElement();
        card.AddToClassList("quintet-card");
        card.AddToClassList(isFive ? "quintet-card--five" : "quintet-card--rookie");

        var posLbl = new Label();
        posLbl.AddToClassList("quintet-card-pos");
        posLbl.text = _posSpanish.TryGetValue(p.Position, out var spanish) ? spanish : p.Position;
        card.Add(posLbl);

        var nameLbl = new Label();
        nameLbl.AddToClassList("quintet-card-name");
        string abbrev = abbrevByKeyword.TryGetValue(p.TeamKeyword, out var a) ? a : "";
        nameLbl.text = string.IsNullOrEmpty(abbrev) ? p.PlayerName : $"{p.PlayerName} ({abbrev})";
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
