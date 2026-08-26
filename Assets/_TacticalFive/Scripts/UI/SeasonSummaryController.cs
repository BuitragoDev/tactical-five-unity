using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;

public class SeasonSummaryController : UIScreenController
{
    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerSeason;
    private Label _headerDate;

    // Summary
    private Label _seasonTag;
    private VisualElement _championLogo;
    private Label _championName;
    private Label _finalsResult;
    private Label _finalsLoser;
    private VisualElement _mvpPhoto;
    private Label _mvpName;
    private Label _mvpTeam;
    private Label _mvpPts;
    private Label _mvpReb;
    private Label _mvpAst;

    // GL Summary
    private VisualElement _glSection;
    private VisualElement _glChampionLogo;
    private Label _glChampionName;
    private Label _glFinalsResult;
    private Label _glFinalsLoser;
    private VisualElement _glMvpPhoto;
    private Label _glMvpName;
    private Label _glMvpTeam;
    private Label _glMvpPts;
    private Label _glMvpReb;
    private Label _glMvpVal;

    private List<TeamData> _allTeams = new();
    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSpritesLarge = new();

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
        _championLogo = _root.Q<VisualElement>("ChampionLogo");
        _championName = _root.Q<Label>("ChampionName");
        _finalsResult = _root.Q<Label>("FinalsResult");
        _finalsLoser = _root.Q<Label>("FinalsLoser");
        _mvpPhoto = _root.Q<VisualElement>("MvpPhoto");
        _mvpName = _root.Q<Label>("MvpName");
        _mvpTeam = _root.Q<Label>("MvpTeam");
        _mvpPts = _root.Q<Label>("MvpPts");
        _mvpReb = _root.Q<Label>("MvpReb");
        _mvpAst = _root.Q<Label>("MvpAst");

        // GL
        _glSection = _root.Q<VisualElement>("GLSection");
        _glChampionLogo = _root.Q<VisualElement>("GLChampionLogo");
        _glChampionName = _root.Q<Label>("GLChampionName");
        _glFinalsResult = _root.Q<Label>("GLFinalsResult");
        _glFinalsLoser = _root.Q<Label>("GLFinalsLoser");
        _glMvpPhoto = _root.Q<VisualElement>("GLMvpPhoto");
        _glMvpName = _root.Q<Label>("GLMvpName");
        _glMvpTeam = _root.Q<Label>("GLMvpTeam");
        _glMvpPts = _root.Q<Label>("GLMvpPts");
        _glMvpReb = _root.Q<Label>("GLMvpReb");
        _glMvpVal = _root.Q<Label>("GLMvpVal");
    }

    protected override void LoadData()
    {
        base.LoadData();
        if (_season == null) return;
        _allTeams = DatabaseManager.Instance.GetAllTeams();

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64)
            _logoSprites[s.name] = s;

        var logos120 = Resources.LoadAll<Sprite>("Teams/Logos/120x120");
        foreach (var s in logos120)
            _logoSpritesLarge[s.name] = s;

        var glLogos64 = Resources.LoadAll<Sprite>("Teams/GLeague/64x64");
        foreach (var s in glLogos64)
            if (!_logoSprites.ContainsKey(s.name))
                _logoSprites[s.name] = s;

        var glLogos120 = Resources.LoadAll<Sprite>("Teams/GLeague/120x120");
        foreach (var s in glLogos120)
            if (!_logoSpritesLarge.ContainsKey(s.name))
                _logoSpritesLarge[s.name] = s;
    }

    protected override void RegisterCallbacks()
    {
        var btnAwards = _root.Q<Button>("BtnGoToAwards");
        if (btnAwards != null)
        {
            btnAwards.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.PlayerAwards); });
            CursorManager.Instance?.RegisterHandCursor(btnAwards);
        }
    }

    protected override void Refresh()
    {
        if (_season == null || _myTeam == null) return;
        RefreshHeader();
        RefreshSummary();
        RefreshGLSummary();
    }

    protected override void RefreshHeader()
    {
        SetTeamLogo(_headerTeamLogo, _myTeam.logo);
        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
        if (!string.IsNullOrEmpty(_season.current_date))
        {
            if (DateTime.TryParse(_season.current_date, out var dt))
                _headerDate.text = dt.ToString("dd/MM/yyyy");
        }
    }

    void RefreshSummary()
    {
        string seasonLabel = $"{_season.year_start}-{_season.year_end.ToString().Substring(2)}";
        _seasonTag.text = seasonLabel;

        var finalsRecords = DatabaseManager.Instance.GetFinalsRecords();
        var currentFinals = finalsRecords.FirstOrDefault(f => f.season == seasonLabel);
        if (currentFinals == null)
        {
            Debug.LogWarning("[SeasonSummary] No finals record found for " + seasonLabel);
            return;
        }

        // Champion
        var champTeam = FindTeam(currentFinals.champ_keyword);
        if (champTeam != null)
        {
            _championName.text = champTeam.name.ToUpper();
            SetTeamLogoLarge(_championLogo, champTeam.logo);
        }
        else
        {
            _championName.text = currentFinals.champ_name.ToUpper();
        }

        _finalsResult.text = currentFinals.result;

        var finalistTeam = FindTeam(currentFinals.finalist_keyword);
        string finalistName = currentFinals.finalist_name ?? "";
        string finalistDisplay = finalistTeam != null ? finalistTeam.name.ToUpper() : finalistName.ToUpper();
        _finalsLoser.text = $"vs {finalistDisplay}";

        // Finals MVP
        if (!string.IsNullOrEmpty(currentFinals.mvp))
        {
            _mvpName.text = currentFinals.mvp;

            var mvpDetails = DatabaseManager.Instance.GetFinalsMVPDetails(_season.id, _manager.id);
            if (mvpDetails != null)
            {
                Texture2D tex = PlayerPhotoHelper.Load(mvpDetails.PlayerId, mvpDetails.Photo);
                _mvpPhoto.style.backgroundImage = new StyleBackground(tex);
                _mvpTeam.text = mvpDetails.TeamName;
                _mvpPts.text = mvpDetails.AvgPts.ToString("F1");
                _mvpReb.text = mvpDetails.AvgReb.ToString("F1");
                _mvpAst.text = mvpDetails.AvgAst.ToString("F1");
            }
        }
    }

    void RefreshGLSummary()
    {
        if (_glSection == null) return;

        var champions = DatabaseManager.Instance.GetGLeagueChampions(_manager.id);
        var glChamp = champions.FirstOrDefault(c => c.season_id == _season.id);

        if (glChamp == null)
        {
            _glSection.style.display = DisplayStyle.None;
            return;
        }

        _glSection.style.display = DisplayStyle.Flex;

        // Champion
        _glChampionName.text = glChamp.team_name.ToUpper();
        if (_logoSpritesLarge.TryGetValue(glChamp.team_name, out var glLogo))
            _glChampionLogo.style.backgroundImage = new StyleBackground(glLogo);

        // Final score
        var finalGame = DatabaseManager.Instance.GetGLFinalGame(_manager.id, _season.id);
        if (finalGame != null)
        {
            int homeId = GLeagueHelper.DecodeGlTeamId(finalGame.home_team_id);
            int awayId = GLeagueHelper.DecodeGlTeamId(finalGame.away_team_id);
            var homeTeam = DatabaseManager.Instance.GetGLeagueTeam(homeId);
            var awayTeam = DatabaseManager.Instance.GetGLeagueTeam(awayId);

            _glFinalsResult.text = $"{finalGame.home_score}-{finalGame.away_score}";

            bool homeWon = finalGame.home_score > finalGame.away_score;
            var loserTeam = homeWon ? awayTeam : homeTeam;
            _glFinalsLoser.text = loserTeam != null ? $"vs {loserTeam.name.ToUpper()}" : "";
        }

        // Season MVP
        var mvp = DatabaseManager.Instance.GetGLSeasonMVP(_manager.id, _season.id);
        if (mvp != null)
        {
            _glMvpName.text = $"{mvp.first_name.ToUpper()} {mvp.last_name.ToUpper()}";
            _glMvpTeam.text = mvp.team_name;
            _glMvpPts.text = mvp.avg_pts.ToString("F1");
            _glMvpReb.text = mvp.avg_reb.ToString("F1");
            _glMvpVal.text = mvp.avg_rating.ToString("F1");

            // Foto: prospects (>=500000) no tienen foto, avatar gris
            if (mvp.player_id >= GLeagueHelper.PROSPECT_ID_OFFSET)
            {
                _glMvpPhoto.style.backgroundImage = null;
                _glMvpPhoto.style.backgroundColor = new Color(0.3f, 0.3f, 0.35f);
            }
            else
            {
                var player = DatabaseManager.Instance.GetPlayerById(mvp.player_id);
                if (player != null)
                {
                    Texture2D tex = PlayerPhotoHelper.Load(player.id, player.photo);
                    _glMvpPhoto.style.backgroundImage = new StyleBackground(tex);
                    _glMvpPhoto.style.backgroundColor = Color.clear;
                }
            }
        }
        else
        {
            _glMvpName.text = "—";
            _glMvpTeam.text = "";
            _glMvpPts.text = "--";
            _glMvpReb.text = "--";
            _glMvpVal.text = "--";
        }
    }

    TeamData FindTeam(string keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return null;
        return _allTeams?.Find(t =>
            t.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(t.logo, keyword, System.StringComparison.OrdinalIgnoreCase));
    }

    void SetTeamLogo(VisualElement elem, string logoName)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;
        if (_logoSprites.TryGetValue(logoName, out var sprite))
            elem.style.backgroundImage = new StyleBackground(sprite);
    }

    void SetTeamLogoLarge(VisualElement elem, string logoName)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;
        if (_logoSpritesLarge.TryGetValue(logoName, out var sprite))
            elem.style.backgroundImage = new StyleBackground(sprite);
        else if (_logoSprites.TryGetValue(logoName, out var fallback))
            elem.style.backgroundImage = new StyleBackground(fallback);
    }
}
