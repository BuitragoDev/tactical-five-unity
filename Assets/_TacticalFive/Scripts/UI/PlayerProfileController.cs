using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class PlayerProfileController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.PlayerProfile;

    private VisualElement _profilePhoto;
    private Label _profilePlayerName;
    private Label _profilePlayerPos;
    private Label _profilePlayerTeam;
    private VisualElement _profileRoleIcon;
    private Label _profileRoleName;
    private Label _profileMeta;
    private Label _profileOvr;

    private VisualElement _profileSeasonSection;
    private VisualElement _profileSeasonBody;
    private VisualElement _profileCareerSection;
    private VisualElement _profileCareerBody;
    private VisualElement _profileEmpty;
    private VisualElement _profileNoStats;

    private PlayerData _player;
    private List<PlayerCareerSeasonRow> _careerHistory;
    private static readonly System.Globalization.CultureInfo _fmt = System.Globalization.CultureInfo.InvariantCulture;
    private static readonly System.Globalization.CultureInfo _spanishCI = new("es-ES");

    protected override void CacheReferences()
    {
        _profilePhoto = _root.Q<VisualElement>("ProfilePhoto");
        _profilePlayerName = _root.Q<Label>("ProfilePlayerName");
        _profilePlayerPos = _root.Q<Label>("ProfilePlayerPos");
        _profilePlayerTeam = _root.Q<Label>("ProfilePlayerTeam");
        _profileRoleIcon = _root.Q<VisualElement>("ProfileRoleIcon");
        _profileRoleName = _root.Q<Label>("ProfileRoleName");
        _profileMeta = _root.Q<Label>("ProfileMeta");
        _profileOvr = _root.Q<Label>("ProfileOvr");
        _profileSeasonSection = _root.Q<VisualElement>("ProfileSeasonSection");
        _profileSeasonBody = _root.Q<VisualElement>("ProfileSeasonBody");
        _profileCareerSection = _root.Q<VisualElement>("ProfileCareerSection");
        _profileCareerBody = _root.Q<VisualElement>("ProfileCareerBody");
        _profileEmpty = _root.Q<VisualElement>("ProfileEmpty");
        _profileNoStats = _root.Q<VisualElement>("ProfileNoStats");
    }

    protected override void LoadData()
    {
        base.LoadData();

        int playerId = ScreenManager.SelectedPlayerId;
        _player = DatabaseManager.Instance.GetPlayerById(playerId);
        if (_player != null)
            _careerHistory = DatabaseManager.Instance.GetPlayerCareerHistory(playerId, _manager.id);
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _root.Q<Button>("PlayerProfileBackBtn")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });
        _root.Q<Button>("ProfileTrajectoryBtn")?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_player == null) return;
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.Trajectory);
        });
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[PlayerProfile] RefreshHeader error: {ex.Message}"); }
        if (_player == null) return;
        FillPlayerHeader();
        BuildSeasonStats();
        BuildCareerTable();
    }

    void FillPlayerHeader()
    {
        _profilePlayerName.text = $"{_player.first_name} {_player.last_name}".ToUpper();
        _profilePlayerPos.text = $"{PositionCodes.GetName(_player.position)} · {PositionCodes.GetName(_player.secondary_position)}";

        var team = DatabaseManager.Instance.GetTeamById(_player.team_id);
        _profilePlayerTeam.text = team?.name ?? "FA";

        UpdateRoleIcon(_player.role);
        _profileRoleName.text = GetRoleName(_player.role);

        _profileMeta.text = $"{_player.age} años · {_player.height_cm / 100f:F2}m · {_player.weight_kg}kg · {CountryCodes.GetName(_player.nationality)}";

        int ovr = _player.GetCalculatedAverage();
        _profileOvr.text = ovr.ToString();

        var tex = PlayerPhotoHelper.Load(_player.id, _player.photo);
        _profilePhoto.style.backgroundImage = tex != null ? new StyleBackground(tex) : StyleKeyword.None;
    }

    void BuildSeasonStats()
    {
        if (_player == null || _season == null)
        {
            _profileSeasonSection.style.display = DisplayStyle.None;
            return;
        }

        var aggregates = DatabaseManager.Instance.GetSeasonPlayerStatsAggregates(_manager.id, _season.id);
        var agg = aggregates.FirstOrDefault(a => a.player_id == _player.id);

        if (agg == null || agg.gp == 0)
        {
            _profileSeasonSection.style.display = DisplayStyle.None;
            _profileNoStats.style.display = DisplayStyle.Flex;
            return;
        }

        _profileSeasonSection.style.display = DisplayStyle.Flex;
        _profileNoStats.style.display = DisplayStyle.None;
        _profileSeasonBody.Clear();

        int g = agg.gp;
        float avgPts = (float)agg.total_points / g;
        float avgReb = (float)agg.total_rebounds / g;
        float avgAst = (float)agg.total_assists / g;
        float avgStl = (float)agg.total_steals / g;
        float avgBlk = (float)agg.total_blocks / g;
        float avgTov = (float)agg.total_turnovers / g;
        float avgMin = (float)agg.total_minutes / g;
        float fgPct = agg.total_fga > 0 ? (float)agg.total_fgm / agg.total_fga * 100f : 0f;
        float fg3Pct = agg.total_fg3a > 0 ? (float)agg.total_fg3m / agg.total_fg3a * 100f : 0f;
        float ftPct = agg.total_fta > 0 ? (float)agg.total_ftm / agg.total_fta * 100f : 0f;
        float efgPct = AdvancedStatsHelper.CalcEFG(agg.total_fgm, agg.total_fga, agg.total_fg3m);
        float tsPct = AdvancedStatsHelper.CalcTS(agg.total_points, agg.total_fga, agg.total_fta);
        var eff = AdvancedStatsHelper.CalcEff(agg.total_points, agg.total_rebounds, agg.total_assists,
                                              agg.total_steals, agg.total_blocks,
                                              agg.total_fgm, agg.total_fga,
                                              agg.total_ftm, agg.total_fta,
                                              agg.total_turnovers);
        float per = AdvancedStatsHelper.CalcPER(eff, (float)agg.total_minutes);

        AddStatCard("PTS", avgPts.ToString("N1", _spanishCI));
        AddStatCard("REB", avgReb.ToString("N1", _spanishCI));
        AddStatCard("AST", avgAst.ToString("N1", _spanishCI));
        AddStatCard("ROB", avgStl.ToString("N2", _spanishCI));
        AddStatCard("TAP", avgBlk.ToString("N2", _spanishCI));
        AddStatCard("TO", avgTov.ToString("N1", _spanishCI));
        AddStatCard("MIN", avgMin.ToString("N1", _spanishCI));
        AddStatCard("TC%", fgPct.ToString("N1", _fmt));
        AddStatCard("3P%", fg3Pct.ToString("N1", _fmt));
        AddStatCard("TL%", ftPct.ToString("N1", _fmt));
        AddStatCardAccent("EFG%", efgPct.ToString("N1", _fmt));
        AddStatCardAccent("TS%", tsPct.ToString("N1", _fmt));
        AddStatCardAccent("PER", per.ToString("N1", _spanishCI));
        AddStatCard("VAL", ((float)agg.total_rating / g).ToString("N1", _spanishCI));
    }

    void AddStatCard(string label, string value)
    {
        var card = new VisualElement();
        card.AddToClassList("playerprofile-stat-card");

        var valLbl = new Label();
        valLbl.AddToClassList("playerprofile-stat-card-value");
        valLbl.text = value;

        var labLbl = new Label();
        labLbl.AddToClassList("playerprofile-stat-card-label");
        labLbl.text = label;

        card.Add(valLbl);
        card.Add(labLbl);
        _profileSeasonBody.Add(card);
    }

    void AddStatCardAccent(string label, string value)
    {
        var card = new VisualElement();
        card.AddToClassList("playerprofile-stat-card");

        var valLbl = new Label();
        valLbl.AddToClassList("playerprofile-stat-card-value--accent");
        valLbl.text = value;

        var labLbl = new Label();
        labLbl.AddToClassList("playerprofile-stat-card-label");
        labLbl.text = label;

        card.Add(valLbl);
        card.Add(labLbl);
        _profileSeasonBody.Add(card);
    }

    void BuildCareerTable()
    {
        if (_careerHistory == null || _careerHistory.Count == 0)
        {
            _profileCareerSection.style.display = DisplayStyle.None;
            _profileEmpty.style.display = DisplayStyle.Flex;
            return;
        }

        _profileCareerSection.style.display = DisplayStyle.Flex;
        _profileEmpty.style.display = DisplayStyle.None;
        _profileCareerBody.Clear();

        var fmt = System.Globalization.CultureInfo.InvariantCulture;

        foreach (var season in _careerHistory)
        {
            var row = new VisualElement();
            row.AddToClassList("playerprofile-table-row");

            row.Add(CreateCell("pph-season", $"{season.year_start}-{season.year_end}"));
            row.Add(CreateCell("pph-team", season.team_name ?? "—"));
            row.Add(CreateCell("pph-gp", season.games.ToString()));
            row.Add(CreateCell("pph-mp", season.games > 0
                ? (season.total_minutes / season.games).ToString("F1", fmt) : "0.0"));
            row.Add(CreateCellBold("pph-pts", season.games > 0
                ? ((float)season.total_points / season.games).ToString("F1", fmt) : "0.0"));
            row.Add(CreateCell("pph-reb", season.games > 0
                ? ((float)season.total_rebounds / season.games).ToString("F1", fmt) : "0.0"));
            row.Add(CreateCell("pph-ast", season.games > 0
                ? ((float)season.total_assists / season.games).ToString("F1", fmt) : "0.0"));
            row.Add(CreateCell("pph-val", season.games > 0
                ? ((float)season.total_rating / season.games).ToString("F1", fmt) : "0.0"));

            _profileCareerBody.Add(row);
        }

        int totalGames = _careerHistory.Sum(s => s.games);
        double totalMinutes = _careerHistory.Sum(s => s.total_minutes);
        int totalPts = _careerHistory.Sum(s => s.total_points);
        int totalReb = _careerHistory.Sum(s => s.total_rebounds);
        int totalAst = _careerHistory.Sum(s => s.total_assists);
        int totalRat = _careerHistory.Sum(s => s.total_rating);

        var totalRow = new VisualElement();
        totalRow.AddToClassList("playerprofile-table-row");
        totalRow.AddToClassList("playerprofile-table-row--totals");

        totalRow.Add(CreateCell("pph-season", "TOTAL"));
        totalRow.Add(CreateCell("pph-team", ""));
        totalRow.Add(CreateCell("pph-gp", totalGames.ToString()));
        totalRow.Add(CreateCell("pph-mp", totalGames > 0
            ? (totalMinutes / totalGames).ToString("F1", fmt) : "0.0"));
        totalRow.Add(CreateCellBold("pph-pts", totalGames > 0
            ? ((float)totalPts / totalGames).ToString("F1", fmt) : "0.0"));
        totalRow.Add(CreateCell("pph-reb", totalGames > 0
            ? ((float)totalReb / totalGames).ToString("F1", fmt) : "0.0"));
        totalRow.Add(CreateCell("pph-ast", totalGames > 0
            ? ((float)totalAst / totalGames).ToString("F1", fmt) : "0.0"));
        totalRow.Add(CreateCell("pph-val", totalGames > 0
            ? ((float)totalRat / totalGames).ToString("F1", fmt) : "0.0"));

        _profileCareerBody.Add(totalRow);
    }

    static Label CreateCell(string className, string text)
    {
        var lbl = new Label();
        lbl.AddToClassList(className);
        lbl.text = text;
        return lbl;
    }

    static Label CreateCellBold(string className, string text)
    {
        var lbl = new Label();
        lbl.AddToClassList("td-pps--bold");
        lbl.text = text;
        return lbl;
    }

    static string GetRoleName(PlayerRole role) => role switch
    {
        PlayerRole.Estrella => "Estrella",
        PlayerRole.Titular => "Titular",
        PlayerRole.Banquillo => "Banquillo",
        _ => "Último recurso"
    };

    void UpdateRoleIcon(PlayerRole role)
    {
        string iconName = role switch
        {
            PlayerRole.Estrella => "rol_estrella",
            PlayerRole.Titular => "rol_titular",
            PlayerRole.Banquillo => "rol_banquillo",
            _ => "rol_ultimoRecurso"
        };
        var tex = Resources.Load<Texture2D>($"Icons/{iconName}");
        _profileRoleIcon.style.backgroundImage = tex != null ? new StyleBackground(tex) : StyleKeyword.None;
        _profileRoleIcon.tooltip = GetRoleName(role);
    }
}
