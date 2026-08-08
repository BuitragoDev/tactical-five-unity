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
    private VisualElement _profileRingsIcon;
    private Label _profileRingsCount;
    private VisualElement _profileFinalsIcon;
    private Label _profileFinalsCount;
    private VisualElement _profileMvpFinalsIcon;
    private Label _profileMvpFinalsCount;
    private VisualElement _profileMvpSeasonIcon;
    private Label _profileMvpSeasonCount;

    private VisualElement _profileSeasonSection;
    private VisualElement _profileSeasonBody;
    private VisualElement _profileAttrsSection;
    private VisualElement _profileAttrsBody;
    private VisualElement _profileAttrsLocked;
    private VisualElement _profileNoStats;

    private PlayerData _player;
    private HashSet<int> _scoutedPlayerIds;
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
        _profileRingsIcon = _root.Q<VisualElement>("ProfileRingsIcon");
        _profileRingsCount = _root.Q<Label>("ProfileRingsCount");
        _profileFinalsIcon = _root.Q<VisualElement>("ProfileFinalsIcon");
        _profileFinalsCount = _root.Q<Label>("ProfileFinalsCount");
        _profileMvpFinalsIcon = _root.Q<VisualElement>("ProfileMvpFinalsIcon");
        _profileMvpFinalsCount = _root.Q<Label>("ProfileMvpFinalsCount");
        _profileMvpSeasonIcon = _root.Q<VisualElement>("ProfileMvpSeasonIcon");
        _profileMvpSeasonCount = _root.Q<Label>("ProfileMvpSeasonCount");
        _profileSeasonSection = _root.Q<VisualElement>("ProfileSeasonSection");
        _profileSeasonBody = _root.Q<VisualElement>("ProfileSeasonBody");
        _profileAttrsSection = _root.Q<VisualElement>("ProfileAttrsSection");
        _profileAttrsBody = _root.Q<VisualElement>("ProfileAttrsBody");
        _profileAttrsLocked = _root.Q<VisualElement>("ProfileAttrsLocked");
        _profileNoStats = _root.Q<VisualElement>("ProfileNoStats");
    }

    protected override void LoadData()
    {
        base.LoadData();

        int playerId = ScreenManager.SelectedPlayerId;
        _player = DatabaseManager.Instance.GetPlayerById(playerId);

        var scouts = DatabaseManager.Instance.GetScoutsByTeam(_myTeam != null ? _myTeam.id : 0);
        _scoutedPlayerIds = new HashSet<int>(
            scouts.Where(s => s.completed == 1).Select(s => s.player_id));
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
        bool canView = FogOfWarHelper.CanViewRatings(_player, _myTeam.id, _scoutedPlayerIds);
        FillPlayerHeader(canView);
        BuildSeasonStats();
        BuildAttrs(canView);
    }

    void FillPlayerHeader(bool canView)
    {
        _profilePlayerName.text = $"{_player.first_name} {_player.last_name}".ToUpper();
        _profilePlayerPos.text = $"{PositionCodes.GetName(_player.position)} · {PositionCodes.GetName(_player.secondary_position)}";

        var team = DatabaseManager.Instance.GetTeamById(_player.team_id);
        _profilePlayerTeam.text = team?.name ?? "FA";

        if (canView)
        {
            UpdateRoleIcon(_player.role);
            _profileRoleName.text = GetRoleName(_player.role);
            _profileRoleIcon.style.display = DisplayStyle.Flex;
        }
        else
        {
            _profileRoleName.text = "?";
            _profileRoleIcon.style.display = DisplayStyle.None;
        }

        _profileMeta.text = $"{_player.age} años · {_player.height_cm / 100f:F2}m · {_player.weight_kg}kg · {CountryCodes.GetName(_player.nationality)}"
            + (_player.rings > 0 ? $"  ·  {_player.rings} anillo{(_player.rings > 1 ? "s" : "")}" : "");

        if (canView)
        {
            _profileOvr.text = _player.GetCalculatedAverage().ToString();
        }
        else
        {
            _profileOvr.text = FogOfWarHelper.GetRatingBand(_player.GetCalculatedAverage(), _player.id);
        }

        var ringsSprite = Resources.Load<Sprite>("Icons/trofeo64px");
        if (ringsSprite != null)
            _profileRingsIcon.style.backgroundImage = new StyleBackground(ringsSprite);
        _profileRingsCount.text = _player.rings.ToString();

        var finalsSprite = Resources.Load<Sprite>("Icons/vs_icon");
        if (finalsSprite != null)
            _profileFinalsIcon.style.backgroundImage = new StyleBackground(finalsSprite);
        _profileFinalsCount.text = _player.finals_played.ToString();

        var mvpFinalsSprite = Resources.Load<Sprite>("Icons/mvp_finals");
        if (mvpFinalsSprite != null)
            _profileMvpFinalsIcon.style.backgroundImage = new StyleBackground(mvpFinalsSprite);
        _profileMvpFinalsCount.text = _player.finals_mvps.ToString();

        var mvpSeasonSprite = Resources.Load<Sprite>("Icons/trofeo_mvp");
        if (mvpSeasonSprite != null)
            _profileMvpSeasonIcon.style.backgroundImage = new StyleBackground(mvpSeasonSprite);
        _profileMvpSeasonCount.text = _player.season_mvps.ToString();

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

    void BuildAttrs(bool canView)
    {
        if (!canView)
        {
            _profileAttrsSection.style.display = DisplayStyle.None;
            _profileAttrsLocked.style.display = DisplayStyle.Flex;
            return;
        }

        _profileAttrsSection.style.display = DisplayStyle.Flex;
        _profileAttrsLocked.style.display = DisplayStyle.None;
        _profileAttrsBody.Clear();

        var attrs = new (string label, int val)[]
        {
            ("TIRO",      _player.shooting),
            ("TRIPLE",    _player.three_point),
            ("PASE",      _player.passing),
            ("BOTE",      _player.dribbling),
            ("DEFENSA",   _player.defense),
            ("REBOTE",    _player.rebounding),
            ("VELOCIDAD", _player.speed),
            ("ATLETISMO", _player.athleticism),
            ("IQ",        _player.iq),
            ("ROBOS",     _player.steals),
            ("TAPONES",   _player.blocks),
            ("MORAL",     _player.morale),
            ("FÍSICO",    _player.fisico),
        };

        foreach (var (label, val) in attrs)
        {
            var card = new VisualElement();
            card.AddToClassList("playerprofile-attr-card");

            var valLbl = new Label();
            valLbl.AddToClassList("playerprofile-attr-card-value");
            if (val >= 70)
                valLbl.AddToClassList("playerprofile-attr-card-value--high");
            else if (val >= 40)
                valLbl.AddToClassList("playerprofile-attr-card-value--mid");
            else
                valLbl.AddToClassList("playerprofile-attr-card-value--low");
            valLbl.text = val.ToString();

            var labLbl = new Label();
            labLbl.AddToClassList("playerprofile-attr-card-label");
            labLbl.text = label;

            card.Add(valLbl);
            card.Add(labLbl);
            _profileAttrsBody.Add(card);
        }
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
