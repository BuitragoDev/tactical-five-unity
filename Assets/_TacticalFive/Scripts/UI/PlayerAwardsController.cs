using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;

public class PlayerAwardsController : UIScreenController
{
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
    private VisualElement _dpoyPlayerPhoto;
    private Label _dpoyPlayerName;
    private Label _dpoyPlayerTeam;
    private Label _dpoyStl;
    private Label _dpoyBlk;
    private VisualElement _sixthPlayerPhoto;
    private Label _sixthPlayerName;
    private Label _sixthPlayerTeam;
    private Label _sixthPts;
    private Label _sixthReb;
    private Label _sixthAst;
    private VisualElement _mipPlayerPhoto;
    private Label _mipPlayerName;
    private Label _mipPlayerTeam;
    private Label _mipNow;
    private Label _mipPrev;
    private VisualElement _coachPlayerPhoto;
    private Label _coachPlayerName;
    private Label _coachPlayerTeam;
    private Label _coachCount;

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
        _dpoyPlayerPhoto = _root.Q<VisualElement>("DpoyPlayerPhoto");
        _dpoyPlayerName = _root.Q<Label>("DpoyPlayerName");
        _dpoyPlayerTeam = _root.Q<Label>("DpoyPlayerTeam");
        _dpoyStl = _root.Q<Label>("DpoyStl");
        _dpoyBlk = _root.Q<Label>("DpoyBlk");
        _sixthPlayerPhoto = _root.Q<VisualElement>("SixthPlayerPhoto");
        _sixthPlayerName = _root.Q<Label>("SixthPlayerName");
        _sixthPlayerTeam = _root.Q<Label>("SixthPlayerTeam");
        _sixthPts = _root.Q<Label>("SixthPts");
        _sixthReb = _root.Q<Label>("SixthReb");
        _sixthAst = _root.Q<Label>("SixthAst");
        _mipPlayerPhoto = _root.Q<VisualElement>("MipPlayerPhoto");
        _mipPlayerName = _root.Q<Label>("MipPlayerName");
        _mipPlayerTeam = _root.Q<Label>("MipPlayerTeam");
        _mipNow = _root.Q<Label>("MipNow");
        _mipPrev = _root.Q<Label>("MipPrev");
        _coachPlayerPhoto = _root.Q<VisualElement>("CoachPlayerPhoto");
        _coachPlayerName = _root.Q<Label>("CoachPlayerName");
        _coachPlayerTeam = _root.Q<Label>("CoachPlayerTeam");
        _coachCount = _root.Q<Label>("CoachCount");
    }

    protected override void LoadData()
    {
        base.LoadData();
    }

    protected override void RegisterCallbacks()
    {
        var btnGo = _root.Q<Button>("BtnGoQuintos");
        if (btnGo != null)
        {
            btnGo.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Quintos); });
            CursorManager.Instance?.RegisterHandCursor(btnGo);
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

        var dpoy = DatabaseManager.Instance.GetBestDefensivePlayer(seasonId, managerId);
        if (dpoy != null)
        {
            string dpoyAbbrev = abbrevByKeyword.TryGetValue(dpoy.TeamKeyword, out var da) ? da : "";
            _dpoyPlayerName.text = string.IsNullOrEmpty(dpoyAbbrev) ? dpoy.PlayerName : $"{dpoy.PlayerName} ({dpoyAbbrev})";
            _dpoyPlayerTeam.text = dpoy.TeamName;
            Texture2D dpoyTex = PlayerPhotoHelper.Load(dpoy.PlayerId, dpoy.Photo);
            _dpoyPlayerPhoto.style.backgroundImage = new StyleBackground(dpoyTex);
            _dpoyStl.text = dpoy.AvgPts.ToString("F1");
            _dpoyBlk.text = dpoy.AvgReb.ToString("F1");
        }

        var sixth = DatabaseManager.Instance.GetSixthMan(seasonId, managerId);
        if (sixth != null)
        {
            string sixthAbbrev = abbrevByKeyword.TryGetValue(sixth.TeamKeyword, out var sa) ? sa : "";
            _sixthPlayerName.text = string.IsNullOrEmpty(sixthAbbrev) ? sixth.PlayerName : $"{sixth.PlayerName} ({sixthAbbrev})";
            _sixthPlayerTeam.text = sixth.TeamName;
            Texture2D sixthTex = PlayerPhotoHelper.Load(sixth.PlayerId, sixth.Photo);
            _sixthPlayerPhoto.style.backgroundImage = new StyleBackground(sixthTex);
            _sixthPts.text = sixth.AvgPts.ToString("F1");
            _sixthReb.text = sixth.AvgReb.ToString("F1");
            _sixthAst.text = sixth.AvgAst.ToString("F1");
        }

        var mip = DatabaseManager.Instance.GetMostImprovedPlayer(seasonId, managerId);
        if (mip != null)
        {
            string mipAbbrev = abbrevByKeyword.TryGetValue(mip.TeamKeyword, out var ia) ? ia : "";
            _mipPlayerName.text = string.IsNullOrEmpty(mipAbbrev) ? mip.PlayerName : $"{mip.PlayerName} ({mipAbbrev})";
            _mipPlayerTeam.text = mip.TeamName;
            Texture2D mipTex = PlayerPhotoHelper.Load(mip.PlayerId, mip.Photo);
            _mipPlayerPhoto.style.backgroundImage = new StyleBackground(mipTex);
            _mipNow.text = mip.AvgPts.ToString("F1");
            _mipPrev.text = mip.AvgReb.ToString("F1");
        }

        var coach = DatabaseManager.Instance.GetCoachOfTheYear(seasonId);
        if (coach != null)
        {
            _coachPlayerName.text = coach.CoachName;
            _coachPlayerTeam.text = coach.TeamName;
            _coachCount.text = coach.RecordText;
            var coachLogos = Resources.LoadAll<Sprite>("Teams/Logos/100x100");
            var coachLogoDict = new Dictionary<string, Sprite>();
            foreach (var s in coachLogos) coachLogoDict[s.name] = s;
            if (coachLogoDict.TryGetValue(coach.TeamKeyword, out var coachSprite))
                _coachPlayerPhoto.style.backgroundImage = new StyleBackground(coachSprite);
            else
                _coachPlayerPhoto.style.backgroundImage = null;
        }
    }
}