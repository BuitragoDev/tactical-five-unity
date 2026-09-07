using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Globalization;

public class SeasonSummaryController : UIScreenController
{
    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerSeason;
    private Label _headerDate;

    // Season intro
    private Label _seasonTag;

    // NBA Champion
    private VisualElement _championLogo;
    private Label _championName;
    private Label _championTitles;

    // GL Champion
    private VisualElement _glChampionLogo;
    private Label _glChampionName;
    private Label _glChampionTitles;

    // Finals MVP
    private VisualElement _mvpPhoto;
    private Label _mvpName;
    private Label _mvpTeam;
    private Label _mvpPts;
    private Label _mvpReb;
    private Label _mvpAst;
    private Label _mvpStl;
    private Label _mvpBlk;

    // GL MVP
    private VisualElement _glMvpPhoto;
    private Label _glMvpName;
    private Label _glMvpTeam;
    private Label _glMvpPts;
    private Label _glMvpReb;
    private Label _glMvpAst;
    private Label _glMvpStl;
    private Label _glMvpBlk;

    // Bottom stats
    private Label _statGamesValue;
    private Label _statAttendanceValue;
    private Label _statAttendanceSub;
    private Label _statOccupancyValue;
    private Label _statOccupancySub;
    private Label _statCountriesValue;
    private VisualElement _statIconGames;
    private VisualElement _statIconAttendance;
    private VisualElement _statIconOccupancy;
    private VisualElement _statIconCountries;

    private List<TeamData> _allTeams = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();
    private Dictionary<string, Sprite> _logoSprites100 = new();
    private static readonly CultureInfo _dotCulture;
    static SeasonSummaryController()
    {
        _dotCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        _dotCulture.NumberFormat.NumberGroupSeparator = ".";
    }

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
        _championTitles = _root.Q<Label>("ChampionTitles");

        _glChampionLogo = _root.Q<VisualElement>("GLChampionLogo");
        _glChampionName = _root.Q<Label>("GLChampionName");
        _glChampionTitles = _root.Q<Label>("GLChampionTitles");

        _mvpPhoto = _root.Q<VisualElement>("MvpPhoto");
        _mvpName = _root.Q<Label>("MvpName");
        _mvpTeam = _root.Q<Label>("MvpTeam");
        _mvpPts = _root.Q<Label>("MvpPts");
        _mvpReb = _root.Q<Label>("MvpReb");
        _mvpAst = _root.Q<Label>("MvpAst");
        _mvpStl = _root.Q<Label>("MvpStl");
        _mvpBlk = _root.Q<Label>("MvpBlk");

        _glMvpPhoto = _root.Q<VisualElement>("GLMvpPhoto");
        _glMvpName = _root.Q<Label>("GLMvpName");
        _glMvpTeam = _root.Q<Label>("GLMvpTeam");
        _glMvpPts = _root.Q<Label>("GLMvpPts");
        _glMvpReb = _root.Q<Label>("GLMvpReb");
        _glMvpAst = _root.Q<Label>("GLMvpAst");
        _glMvpStl = _root.Q<Label>("GLMvpStl");
        _glMvpBlk = _root.Q<Label>("GLMvpBlk");

        _statGamesValue = _root.Q<Label>("StatGamesValue");
        _statAttendanceValue = _root.Q<Label>("StatAttendanceValue");
        _statAttendanceSub = _root.Q<Label>("StatAttendanceSub");
        _statOccupancyValue = _root.Q<Label>("StatOccupancyValue");
        _statOccupancySub = _root.Q<Label>("StatOccupancySub");
        _statCountriesValue = _root.Q<Label>("StatCountriesValue");
        _statIconGames = _root.Q<VisualElement>("StatIconGames");
        _statIconAttendance = _root.Q<VisualElement>("StatIconAttendance");
        _statIconOccupancy = _root.Q<VisualElement>("StatIconOccupancy");
        _statIconCountries = _root.Q<VisualElement>("StatIconCountries");
    }

    protected override void LoadData()
    {
        base.LoadData();
        if (_season == null) return;
        _allTeams = DatabaseManager.Instance.GetAllTeams();

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        var logos100 = Resources.LoadAll<Sprite>("Teams/Logos/100x100");
        foreach (var s in logos100) _logoSprites100[s.name] = s;

        var glLogos64 = Resources.LoadAll<Sprite>("Teams/GLeague/64x64");
        foreach (var s in glLogos64)
            if (!_logoSprites64.ContainsKey(s.name)) _logoSprites64[s.name] = s;

        var glLogos100 = Resources.LoadAll<Sprite>("Teams/GLeague/100x100");
        foreach (var s in glLogos100)
            if (!_logoSprites100.ContainsKey(s.name)) _logoSprites100[s.name] = s;
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
        RefreshContent();
    }

    protected override void RefreshHeader()
    {
        SetLogo64(_headerTeamLogo, _myTeam.logo);
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

        LoadTrophies();
        RefreshNBAChampion(seasonLabel);
        RefreshGLChampion();
        RefreshFinalsMVP();
        RefreshGLMVP();
        RefreshBottomStats(seasonLabel);
    }

    void LoadTrophies()
    {
        var basketTex = Resources.Load<Texture2D>("Icons/basket_icon");
        if (basketTex != null) _statIconGames.style.backgroundImage = new StyleBackground(basketTex);

        var asistenciaTex = Resources.Load<Texture2D>("Icons/asistencia_icon");
        if (asistenciaTex != null) _statIconAttendance.style.backgroundImage = new StyleBackground(asistenciaTex);

        var ocupacionTex = Resources.Load<Texture2D>("Icons/ocupacion_icon");
        if (ocupacionTex != null) _statIconOccupancy.style.backgroundImage = new StyleBackground(ocupacionTex);

        var globalTex = Resources.Load<Texture2D>("Icons/global_icon");
        if (globalTex != null) _statIconCountries.style.backgroundImage = new StyleBackground(globalTex);
    }

    void RefreshNBAChampion(string seasonLabel)
    {
        var finalsRecords = DatabaseManager.Instance.GetFinalsRecords();
        var currentFinals = finalsRecords.FirstOrDefault(f => f.season == seasonLabel);
        if (currentFinals == null)
        {
            _championName.text = "—";
            _championTitles.text = "";
            return;
        }

        var champTeam = FindTeam(currentFinals.champ_keyword);
        if (champTeam != null)
        {
            _championName.text = champTeam.name.ToUpper();
            SetLogo100(_championLogo, champTeam.logo);

            int titleCount = finalsRecords.Count(f => f.champ_name == currentFinals.champ_name);
            _championTitles.text = titleCount == 1
                ? "1.er TÍTULO DE LA FRANQUICIA"
                : $"{titleCount}º TÍTULO DE LA FRANQUICIA";
        }
        else
        {
            _championName.text = currentFinals.champ_name.ToUpper();
            _championTitles.text = "";
        }
    }

    void RefreshGLChampion()
    {
        if (_season != null)
            GLeaguePostSeason.CompletePostSeason(_season, _manager.id);

        var champions = DatabaseManager.Instance.GetGLeagueChampions(_manager.id);
        var glChamp = champions.FirstOrDefault(c => c.season_id == _season.id);

        if (glChamp != null)
        {
            _glChampionName.text = glChamp.team_name.ToUpper();

            var championTeam = DatabaseManager.Instance.GetGLeagueTeam(glChamp.gleague_team_id);
            if (championTeam != null && _logoSprites100.TryGetValue(championTeam.logo, out var glLogo))
                _glChampionLogo.style.backgroundImage = new StyleBackground(glLogo);

            int titleCount = champions.Count(c => c.team_name == glChamp.team_name);
            _glChampionTitles.text = titleCount == 1
                ? "1.er TÍTULO DE LA FRANQUICIA"
                : $"{titleCount}º TÍTULO DE LA FRANQUICIA";
        }
        else
        {
            _glChampionName.text = "—";
            _glChampionTitles.text = "";
        }
    }

    void RefreshFinalsMVP()
    {
        var finalsRecords = DatabaseManager.Instance.GetFinalsRecords();
        string seasonLabel = $"{_season.year_start}-{_season.year_end.ToString().Substring(2)}";
        var currentFinals = finalsRecords.FirstOrDefault(f => f.season == seasonLabel);

        if (currentFinals == null || string.IsNullOrEmpty(currentFinals.mvp))
        {
            _mvpName.text = "—";
            _mvpTeam.text = "";
            return;
        }

        _mvpName.text = currentFinals.mvp.ToUpper();

        var mvpDetails = DatabaseManager.Instance.GetFinalsMVPDetails(_season.id, _manager.id);
        if (mvpDetails != null)
        {
            Texture2D tex = PlayerPhotoHelper.Load(mvpDetails.PlayerId, mvpDetails.Photo);
            if (tex != null) _mvpPhoto.style.backgroundImage = new StyleBackground(tex);

            _mvpTeam.text = string.IsNullOrEmpty(mvpDetails.TeamName) ? "" : mvpDetails.TeamName.ToUpper();
            _mvpPts.text = mvpDetails.AvgPts.ToString("F1", _dotCulture);
            _mvpReb.text = mvpDetails.AvgReb.ToString("F1", _dotCulture);
            _mvpAst.text = mvpDetails.AvgAst.ToString("F1", _dotCulture);
            _mvpStl.text = mvpDetails.AvgStl.ToString("F1", _dotCulture);
            _mvpBlk.text = mvpDetails.AvgBlk.ToString("F1", _dotCulture);
        }
    }

    void RefreshGLMVP()
    {
        var mvp = DatabaseManager.Instance.GetGLSeasonMVP(_manager.id, _season.id);
        if (mvp != null)
        {
            _glMvpName.text = $"{mvp.first_name.ToUpper()} {mvp.last_name.ToUpper()}";
            _glMvpTeam.text = string.IsNullOrEmpty(mvp.team_name) ? "" : mvp.team_name.ToUpper();
            _glMvpPts.text = mvp.avg_pts.ToString("F1", _dotCulture);
            _glMvpReb.text = mvp.avg_reb.ToString("F1", _dotCulture);
            _glMvpAst.text = mvp.avg_ast.ToString("F1", _dotCulture);
            _glMvpStl.text = mvp.avg_stl.ToString("F1", _dotCulture);
            _glMvpBlk.text = mvp.avg_blk.ToString("F1", _dotCulture);

            if (mvp.player_id >= GLeagueHelper.PROSPECT_ID_OFFSET)
            {
                int prospectId = mvp.player_id - GLeagueHelper.PROSPECT_ID_OFFSET;
                var glPlayer = DatabaseManager.Instance.GetGLeaguePlayerById(prospectId);
                if (glPlayer != null && !string.IsNullOrEmpty(glPlayer.photo))
                {
                    Texture2D tex = Resources.Load<Texture2D>($"PlayerPhotos/{glPlayer.photo}");
                    if (tex != null) _glMvpPhoto.style.backgroundImage = new StyleBackground(tex);
                }
            }
            else
            {
                var player = DatabaseManager.Instance.GetPlayerById(mvp.player_id);
                if (player != null)
                {
                    Texture2D tex = PlayerPhotoHelper.Load(player.id, player.photo);
                    if (tex != null) _glMvpPhoto.style.backgroundImage = new StyleBackground(tex);
                }
            }
        }
        else
        {
            _glMvpName.text = "—";
            _glMvpTeam.text = "";
        }
    }

    void RefreshBottomStats(string seasonLabel)
    {
        var db = DatabaseManager.Instance.Db;

        // Games played (regular + playoff)
        int regGames = db.Table<GameData>()
            .Where(g => g.manager_id == _manager.id && g.season_id == _season.id
                     && g.is_played == 1 && g.game_type == "regular")
            .Count();
        int poGames = db.Table<GameData>()
            .Where(g => g.manager_id == _manager.id && g.season_id == _season.id
                     && g.is_played == 1 && g.game_type == "playoff")
            .Count();
        _statGamesValue.text = (regGames + poGames).ToString("#,##0", _dotCulture);

        // Attendance
        var seasonGameIds = db.Table<GameData>()
            .Where(g => g.manager_id == _manager.id && g.season_id == _season.id && g.is_played == 1)
            .Select(g => g.id)
            .ToList();

        long totalAttendance = 0;
        long totalCapacity = 0;
        foreach (var gid in seasonGameIds)
        {
            var att = db.Table<GameAttendanceData>().Where(a => a.game_id == gid).FirstOrDefault();
            if (att != null && att.attendance > 0)
            {
                totalAttendance += att.attendance;
                var game = db.Table<GameData>().Where(g => g.id == gid).FirstOrDefault();
                if (game != null)
                {
                    var homeTeam = db.Table<TeamData>().Where(t => t.id == game.home_team_id).FirstOrDefault();
                    if (homeTeam != null) totalCapacity += homeTeam.capacity;
                }
            }
        }

        _statAttendanceValue.text = totalAttendance.ToString("#,##0", _dotCulture);
        _statAttendanceSub.text = "Temporada regular + Playoffs";

        // Occupancy
        float occupancy = totalCapacity > 0 ? (float)totalAttendance / totalCapacity * 100f : 0;
        _statOccupancyValue.text = $"{occupancy.ToString("F1", _dotCulture)}%";

        // % vs previous season
        var prevSummary = db.Table<SeasonAttendanceSummaryData>()
            .Where(s => s.manager_id == _manager.id && s.season_id < _season.id)
            .OrderByDescending(s => s.season_id)
            .FirstOrDefault();

        if (prevSummary != null && prevSummary.total_attendance > 0)
        {
            float attDelta = prevSummary.total_attendance > 0
                ? ((float)totalAttendance - prevSummary.total_attendance) / prevSummary.total_attendance * 100f
                : 0;
            float occDelta = prevSummary.occupancy_pct > 0
                ? occupancy - prevSummary.occupancy_pct
                : 0;

            _statAttendanceSub.text = $"{FormatDelta(attDelta)} vs. temporada pasada";
            _statAttendanceSub.style.color = attDelta >= 0
                ? new Color(0.31f, 0.71f, 0.39f)
                : new Color(0.9f, 0.3f, 0.3f);

            _statOccupancySub.text = $"{FormatDelta(occDelta)} vs. temporada pasada";
            _statOccupancySub.style.color = occDelta >= 0
                ? new Color(0.31f, 0.71f, 0.39f)
                : new Color(0.9f, 0.3f, 0.3f);
        }
        else
        {
            _statAttendanceSub.text = "+0.0% vs. temporada pasada";
            _statAttendanceSub.style.color = new Color(0.31f, 0.71f, 0.39f);
            _statOccupancySub.text = "+0.0% vs. temporada pasada";
            _statOccupancySub.style.color = new Color(0.31f, 0.71f, 0.39f);
        }

        // Countries represented
        int countries = db.Table<PlayerData>()
            .Where(p => p.team_id > 0 && !string.IsNullOrEmpty(p.nationality))
            .Select(p => p.nationality)
            .Distinct()
            .Count();
        _statCountriesValue.text = countries.ToString();
    }

    string FormatDelta(float delta)
    {
        string sign = delta >= 0 ? "+" : "";
        return $"{sign}{delta.ToString("F1", _dotCulture)}%";
    }

    TeamData FindTeam(string keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return null;
        return _allTeams?.Find(t =>
            t.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(t.logo, keyword, System.StringComparison.OrdinalIgnoreCase));
    }

    void SetLogo64(VisualElement elem, string logoName)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;
        if (_logoSprites64.TryGetValue(logoName, out var sprite))
            elem.style.backgroundImage = new StyleBackground(sprite);
    }

    void SetLogo100(VisualElement elem, string logoName)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;
        if (_logoSprites100.TryGetValue(logoName, out var sprite))
            elem.style.backgroundImage = new StyleBackground(sprite);
        else if (_logoSprites64.TryGetValue(logoName, out var fallback))
            elem.style.backgroundImage = new StyleBackground(fallback);
    }
}
