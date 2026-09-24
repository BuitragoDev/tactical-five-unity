using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;
using System.Linq;

public class PlayerAwardsController : UIScreenController
{
    // ── Header ──
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerSeason;
    private Label _headerDate;

    // ── Screen Header ──
    private Label _seasonTag;

    // ── MVP ──
    private VisualElement _mvpBadgeIcon;
    private VisualElement _mvpPhoto;
    private VisualElement _mvpTeamLogo;
    private Label _mvpName;
    private Label _mvpTeam;
    private Label _mvpPts;
    private Label _mvpReb;
    private Label _mvpAst;

    // ── DPOY ──
    private VisualElement _dpoyBadgeIcon;
    private VisualElement _dpoyPhoto;
    private VisualElement _dpoyTeamLogo;
    private Label _dpoyName;
    private Label _dpoyTeam;
    private Label _dpoyPts;
    private Label _dpoyReb;

    // ── Rookie ──
    private VisualElement _rookieBadgeIcon;
    private VisualElement _rookiePhoto;
    private VisualElement _rookieTeamLogo;
    private Label _rookieName;
    private Label _rookieTeam;
    private Label _rookiePts;
    private Label _rookieReb;
    private Label _rookieAst;

    // ── Sixth Man ──
    private VisualElement _sixthBadgeIcon;
    private VisualElement _sixthPhoto;
    private VisualElement _sixthTeamLogo;
    private Label _sixthName;
    private Label _sixthTeam;
    private Label _sixthPts;
    private Label _sixthReb;
    private Label _sixthAst;

    // ── MIP ──
    private VisualElement _mipBadgeIcon;
    private VisualElement _mipPhoto;
    private VisualElement _mipTeamLogo;
    private Label _mipName;
    private Label _mipTeam;
    private Label _mipPts;
    private Label _mipReb;
    private Label _mipAst;

    // ── Coach ──
    private VisualElement _coachBadgeIcon;
    private VisualElement _coachPhoto;
    private Label _coachName;
    private Label _coachTeam;
    private Label _coachRecord;

    // ── Best Team ──
    private VisualElement _bestBadgeIcon;
    private VisualElement _bestTeamLogo;
    private Label _bestTeamName;
    private Label _bestTeamRecord;

    // ── Worst Team ──
    private VisualElement _worstBadgeIcon;
    private VisualElement _worstTeamLogo;
    private Label _worstTeamName;
    private Label _worstTeamRecord;

    // ── Caches ──
    private Dictionary<string, Sprite> _logos64;
    private Dictionary<string, Sprite> _logos100;

    protected override void OnEnable()
    {
        base.OnEnable();
        CursorManager.Instance?.SetDefaultCursor();
    }

    protected override void CacheReferences()
    {
        // Header
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");

        // Screen Header
        _seasonTag = _root.Q<Label>("SeasonTag");

        // MVP
        _mvpBadgeIcon = _root.Q<VisualElement>("MvpBadgeIcon");
        _mvpPhoto = _root.Q<VisualElement>("MvpPhoto");
        _mvpTeamLogo = _root.Q<VisualElement>("MvpTeamLogo");
        _mvpName = _root.Q<Label>("MvpName");
        _mvpTeam = _root.Q<Label>("MvpTeam");
        _mvpPts = _root.Q<Label>("MvpPts");
        _mvpReb = _root.Q<Label>("MvpReb");
        _mvpAst = _root.Q<Label>("MvpAst");

        // DPOY
        _dpoyBadgeIcon = _root.Q<VisualElement>("DpoyBadgeIcon");
        _dpoyPhoto = _root.Q<VisualElement>("DpoyPhoto");
        _dpoyTeamLogo = _root.Q<VisualElement>("DpoyTeamLogo");
        _dpoyName = _root.Q<Label>("DpoyName");
        _dpoyTeam = _root.Q<Label>("DpoyTeam");
        _dpoyPts = _root.Q<Label>("DpoyPts");
        _dpoyReb = _root.Q<Label>("DpoyReb");

        // Rookie
        _rookieBadgeIcon = _root.Q<VisualElement>("RookieBadgeIcon");
        _rookiePhoto = _root.Q<VisualElement>("RookiePhoto");
        _rookieTeamLogo = _root.Q<VisualElement>("RookieTeamLogo");
        _rookieName = _root.Q<Label>("RookieName");
        _rookieTeam = _root.Q<Label>("RookieTeam");
        _rookiePts = _root.Q<Label>("RookiePts");
        _rookieReb = _root.Q<Label>("RookieReb");
        _rookieAst = _root.Q<Label>("RookieAst");

        // Sixth Man
        _sixthBadgeIcon = _root.Q<VisualElement>("SixthBadgeIcon");
        _sixthPhoto = _root.Q<VisualElement>("SixthPhoto");
        _sixthTeamLogo = _root.Q<VisualElement>("SixthTeamLogo");
        _sixthName = _root.Q<Label>("SixthName");
        _sixthTeam = _root.Q<Label>("SixthTeam");
        _sixthPts = _root.Q<Label>("SixthPts");
        _sixthReb = _root.Q<Label>("SixthReb");
        _sixthAst = _root.Q<Label>("SixthAst");

        // MIP
        _mipBadgeIcon = _root.Q<VisualElement>("MipBadgeIcon");
        _mipPhoto = _root.Q<VisualElement>("MipPhoto");
        _mipTeamLogo = _root.Q<VisualElement>("MipTeamLogo");
        _mipName = _root.Q<Label>("MipName");
        _mipTeam = _root.Q<Label>("MipTeam");
        _mipPts = _root.Q<Label>("MipPts");
        _mipReb = _root.Q<Label>("MipReb");
        _mipAst = _root.Q<Label>("MipAst");

        // Coach
        _coachBadgeIcon = _root.Q<VisualElement>("CoachBadgeIcon");
        _coachPhoto = _root.Q<VisualElement>("CoachPhoto");
        _coachName = _root.Q<Label>("CoachName");
        _coachTeam = _root.Q<Label>("CoachTeam");
        _coachRecord = _root.Q<Label>("CoachRecord");

        // Best Team
        _bestBadgeIcon = _root.Q<VisualElement>("BestBadgeIcon");
        _bestTeamLogo = _root.Q<VisualElement>("BestTeamLogo");
        _bestTeamName = _root.Q<Label>("BestTeamName");
        _bestTeamRecord = _root.Q<Label>("BestTeamRecord");

        // Worst Team
        _worstBadgeIcon = _root.Q<VisualElement>("WorstBadgeIcon");
        _worstTeamLogo = _root.Q<VisualElement>("WorstTeamLogo");
        _worstTeamName = _root.Q<Label>("WorstTeamName");
        _worstTeamRecord = _root.Q<Label>("WorstTeamRecord");
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

        LoadLogoCaches();
        RefreshHeader();
        RefreshScreenHeader();
        RefreshPlayerAwards();
        RefreshTeamAwards();
    }

    // ── Logo caches ──
    void LoadLogoCaches()
    {
        _logos64 = new Dictionary<string, Sprite>();
        foreach (var s in TeamImages.Logos(64))
            _logos64[s.name] = s;

        _logos100 = new Dictionary<string, Sprite>();
        foreach (var s in TeamImages.Logos(100))
            _logos100[s.name] = s;
    }

    // ── Header ──
    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logos64.TryGetValue(_myTeam.logo, out var sprite))
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

    // ── Screen Header ──
    void RefreshScreenHeader()
    {
        string seasonLabel = $"{_season.year_start}-{_season.year_end.ToString().Substring(2)}";
        _seasonTag.text = seasonLabel;
    }

    // ── Badge icons ──
    void LoadBadgeIcon(VisualElement icon, string resourceName)
    {
        var tex = Resources.Load<Texture2D>($"Icons/{resourceName}");
        if (tex != null)
            icon.style.backgroundImage = new StyleBackground(tex);
    }

    // ── Player awards ──
    void RefreshPlayerAwards()
    {
        int seasonId = _season.id;
        int managerId = _manager.id;

        // Badge icons
        LoadBadgeIcon(_mvpBadgeIcon, "mejor_jugador");
        LoadBadgeIcon(_dpoyBadgeIcon, "mejor_defensor");
        LoadBadgeIcon(_rookieBadgeIcon, "mejor_novato");
        LoadBadgeIcon(_sixthBadgeIcon, "mejor_sexto");
        LoadBadgeIcon(_mipBadgeIcon, "estadisticas");

        // MVP
        var mvp = DatabaseManager.Instance.GetRegularSeasonMVP(seasonId, managerId);
        if (mvp != null)
        {
            _mvpName.text = mvp.PlayerName.ToUpper();
            _mvpTeam.text = mvp.TeamName.ToUpper();
            _mvpPts.text = mvp.AvgPts.ToString("F1");
            _mvpReb.text = mvp.AvgReb.ToString("F1");
            _mvpAst.text = mvp.AvgAst.ToString("F1");
            SetPlayerPhoto(_mvpPhoto, mvp.PlayerId, mvp.Photo);
            SetTeamLogoOverlay(_mvpTeamLogo, mvp.TeamKeyword);
        }

        // DPOY — avg_pts=steals, avg_reb=blocks, avg_ast=0
        var dpoy = DatabaseManager.Instance.GetBestDefensivePlayer(seasonId, managerId);
        if (dpoy != null)
        {
            _dpoyName.text = dpoy.PlayerName.ToUpper();
            _dpoyTeam.text = dpoy.TeamName.ToUpper();
            _dpoyPts.text = dpoy.AvgPts.ToString("F1");
            _dpoyReb.text = dpoy.AvgReb.ToString("F1");
            SetPlayerPhoto(_dpoyPhoto, dpoy.PlayerId, dpoy.Photo);
            SetTeamLogoOverlay(_dpoyTeamLogo, dpoy.TeamKeyword);
        }

        // Rookie
        var rookie = DatabaseManager.Instance.GetRookieOfYear(seasonId, managerId);
        if (rookie != null)
        {
            _rookieName.text = rookie.PlayerName.ToUpper();
            _rookieTeam.text = rookie.TeamName.ToUpper();
            _rookiePts.text = rookie.AvgPts.ToString("F1");
            _rookieReb.text = rookie.AvgReb.ToString("F1");
            _rookieAst.text = rookie.AvgAst.ToString("F1");
            SetPlayerPhoto(_rookiePhoto, rookie.PlayerId, rookie.Photo);
            SetTeamLogoOverlay(_rookieTeamLogo, rookie.TeamKeyword);
        }

        // Sixth Man
        var sixth = DatabaseManager.Instance.GetSixthMan(seasonId, managerId);
        if (sixth != null)
        {
            _sixthName.text = sixth.PlayerName.ToUpper();
            _sixthTeam.text = sixth.TeamName.ToUpper();
            _sixthPts.text = sixth.AvgPts.ToString("F1");
            _sixthReb.text = sixth.AvgReb.ToString("F1");
            _sixthAst.text = sixth.AvgAst.ToString("F1");
            SetPlayerPhoto(_sixthPhoto, sixth.PlayerId, sixth.Photo);
            SetTeamLogoOverlay(_sixthTeamLogo, sixth.TeamKeyword);
        }

        // MIP
        var mip = DatabaseManager.Instance.GetMostImprovedPlayer(seasonId, managerId);
        if (mip != null)
        {
            _mipName.text = mip.PlayerName.ToUpper();
            _mipTeam.text = mip.TeamName.ToUpper();
            _mipPts.text = mip.AvgPts.ToString("F1");
            _mipReb.text = mip.AvgReb.ToString("F1");
            _mipAst.text = mip.AvgAst.ToString("F1");
            SetPlayerPhoto(_mipPhoto, mip.PlayerId, mip.Photo);
            SetTeamLogoOverlay(_mipTeamLogo, mip.TeamKeyword);
        }
    }

    // ── Team / Staff awards ──
    void RefreshTeamAwards()
    {
        int seasonId = _season.id;
        int managerId = _manager.id;

        // Badge icons
        LoadBadgeIcon(_coachBadgeIcon, "mejor_entrenador");
        LoadBadgeIcon(_bestBadgeIcon, "mejor_equipo");
        LoadBadgeIcon(_worstBadgeIcon, "peor_equipo");

        // Coach
        var coach = DatabaseManager.Instance.GetCoachOfTheYear(seasonId);
        if (coach != null)
        {
            _coachName.text = coach.CoachName.ToUpper();
            _coachTeam.text = coach.TeamName.ToUpper();
            _coachRecord.text = coach.RecordText;
            if (_logos100.TryGetValue(coach.TeamKeyword, out var coachLogo))
                _coachPhoto.style.backgroundImage = new StyleBackground(coachLogo);
        }

        // Compute standings for best/worst team
        var games = DatabaseManager.Instance.GetStandingsGames(managerId);
        var allTeams = DatabaseManager.Instance.GetAllTeams();

        var winsDict = new Dictionary<int, int>();
        var lossesDict = new Dictionary<int, int>();
        var pfDict = new Dictionary<int, int>();
        var paDict = new Dictionary<int, int>();
        foreach (var t in allTeams)
        {
            winsDict[t.id] = 0;
            lossesDict[t.id] = 0;
            pfDict[t.id] = 0;
            paDict[t.id] = 0;
        }

        foreach (var g in games)
        {
            if (winsDict.ContainsKey(g.home_team_id))
            {
                bool homeWon = g.home_score > g.away_score;
                winsDict[g.home_team_id] += homeWon ? 1 : 0;
                lossesDict[g.home_team_id] += homeWon ? 0 : 1;
                pfDict[g.home_team_id] += g.home_score;
                paDict[g.home_team_id] += g.away_score;
            }
            if (winsDict.ContainsKey(g.away_team_id))
            {
                bool awayWon = g.away_score > g.home_score;
                winsDict[g.away_team_id] += awayWon ? 1 : 0;
                lossesDict[g.away_team_id] += awayWon ? 0 : 1;
                pfDict[g.away_team_id] += g.away_score;
                paDict[g.away_team_id] += g.home_score;
            }
        }

        var sorted = allTeams
            .Where(t => winsDict[t.id] + lossesDict[t.id] > 0)
            .OrderByDescending(t => (float)winsDict[t.id] / (winsDict[t.id] + lossesDict[t.id]))
            .ThenBy(t => lossesDict[t.id])
            .ThenByDescending(t => winsDict[t.id])
            .ThenByDescending(t => pfDict[t.id] - paDict[t.id])
            .ToList();

        // Best team
        if (sorted.Count > 0)
        {
            var best = sorted[0];
            _bestTeamName.text = best.name.ToUpper();
            _bestTeamRecord.text = $"{winsDict[best.id]} - {lossesDict[best.id]}";
            if (_logos100.TryGetValue(best.logo, out var bestLogo))
                _bestTeamLogo.style.backgroundImage = new StyleBackground(bestLogo);
        }

        // Worst team
        if (sorted.Count > 0)
        {
            var worst = sorted[sorted.Count - 1];
            _worstTeamName.text = worst.name.ToUpper();
            _worstTeamRecord.text = $"{winsDict[worst.id]} - {lossesDict[worst.id]}";
            if (_logos100.TryGetValue(worst.logo, out var worstLogo))
                _worstTeamLogo.style.backgroundImage = new StyleBackground(worstLogo);
        }
    }

    // ── Helpers ──
    void SetPlayerPhoto(VisualElement photoEl, int playerId, string photoField)
    {
        Texture2D tex = PlayerPhotoHelper.Load(playerId, photoField);
        if (tex != null)
            photoEl.style.backgroundImage = new StyleBackground(tex);
    }

    void SetTeamLogoOverlay(VisualElement logoEl, string teamKeyword)
    {
        if (!string.IsNullOrEmpty(teamKeyword) && _logos64.TryGetValue(teamKeyword, out var logo))
            logoEl.style.backgroundImage = new StyleBackground(logo);
    }
}
