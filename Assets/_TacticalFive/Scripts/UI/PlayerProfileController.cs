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
    private Label _profileNumber;
    private VisualElement _profileTeamLogo;
    private VisualElement _profileBannerBg;
    private VisualElement _profileOvrBlock;
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
    private VisualElement _profileInfoBody;
    private VisualElement _profileContractBody;
    private VisualElement _profileInjuries;
    private VisualElement _profileInjuriesBody;
    private VisualElement _profileAttrsSection;
    private VisualElement _profileAttrsBody;
    private VisualElement _profileAttrsLocked;
    private VisualElement _profileNoStats;

    private Dictionary<string, Sprite> _teamLogos = new();

    private PlayerData _player;
    private HashSet<int> _scoutedPlayerIds;
    // Formato único del panel (es-ES): antes los porcentajes usaban InvariantCulture y
    // salían con punto decimal mezclados con el resto con coma.

    protected override void CacheReferences()
    {
        _profilePhoto = _root.Q<VisualElement>("ProfilePhoto");
        _profilePlayerName = _root.Q<Label>("ProfilePlayerName");
        _profilePlayerPos = _root.Q<Label>("ProfilePlayerPos");
        _profilePlayerTeam = _root.Q<Label>("ProfilePlayerTeam");
        _profileNumber = _root.Q<Label>("ProfileNumber");
        _profileTeamLogo = _root.Q<VisualElement>("ProfileTeamLogo");
        _profileBannerBg = _root.Q<VisualElement>("ProfileBannerBg");
        _profileOvrBlock = _root.Q<VisualElement>("ProfileOvrBlock");
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
        _profileInfoBody = _root.Q<VisualElement>("ProfileInfoBody");
        _profileContractBody = _root.Q<VisualElement>("ProfileContractBody");
        _profileInjuries = _root.Q<VisualElement>("ProfileInjuries");
        _profileInjuriesBody = _root.Q<VisualElement>("ProfileInjuriesBody");
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

        _scoutedPlayerIds = DatabaseManager.Instance.GetScoutedPlayerIds(_myTeam != null ? _myTeam.id : 0);

        _teamLogos.Clear();
        foreach (var s in TeamImages.Logos(64))
            _teamLogos[s.name] = s;
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
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
        BuildPersonalInfo();
        BuildContractInfo();
        BuildInjuryHistory();
        BuildAttrs(canView);
    }

    void FillPlayerHeader(bool canView)
    {
        _profilePlayerName.text = $"{_player.first_name} {_player.last_name}".ToUpper();
        _profilePlayerPos.text = $"{PositionCodes.GetName(_player.position)} · {PositionCodes.GetName(_player.secondary_position)}";

        var team = DatabaseManager.Instance.GetTeamById(_player.team_id);
        _profilePlayerTeam.text = team?.name ?? "Agente libre";

        _profileNumber.text = _player.number > 0 ? $"#{_player.number}" : "";
        _profileNumber.style.display = _player.number > 0 ? DisplayStyle.Flex : DisplayStyle.None;

        if (team != null && _teamLogos.TryGetValue(team.logo, out var logoSprite))
            _profileTeamLogo.style.backgroundImage = new StyleBackground(logoSprite);
        else
            _profileTeamLogo.style.backgroundImage = StyleKeyword.None;

        // Imagen del pabellón del equipo como fondo del banner.
        if (_profileBannerBg != null)
        {
            string arenaKey = team?.logo;
            if (arenaKey == "sixers") arenaKey = "76ers"; // desajuste de nombre de archivo de arena
            var arenaSprite = !string.IsNullOrEmpty(arenaKey) ? TeamImages.Arena(arenaKey) : null;
            if (arenaSprite == null) arenaSprite = TeamImages.Arena("default");
            _profileBannerBg.style.backgroundImage = arenaSprite != null ? new StyleBackground(arenaSprite) : StyleKeyword.None;
        }

        int ovr = _player.GetCalculatedAverage();
        if (canView)
            _profileOvr.text = ovr.ToString();
        else
            _profileOvr.text = FogOfWarHelper.GetRatingBand(ovr, _player.id);

        // MED por rango (como los atributos): <40 rojo, <70 dorado, resto verde.
        // Con fog-of-war (sin ver ratings) el badge queda neutro.
        if (_profileOvrBlock != null)
        {
            _profileOvrBlock.RemoveFromClassList("playerprofile-ovr-block--high");
            _profileOvrBlock.RemoveFromClassList("playerprofile-ovr-block--mid");
            _profileOvrBlock.RemoveFromClassList("playerprofile-ovr-block--low");
            if (canView)
            {
                if (ovr < 40) _profileOvrBlock.AddToClassList("playerprofile-ovr-block--low");
                else if (ovr < 70) _profileOvrBlock.AddToClassList("playerprofile-ovr-block--mid");
                else _profileOvrBlock.AddToClassList("playerprofile-ovr-block--high");
            }
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

        var mvpSeasonSprite = Resources.Load<Sprite>("Icons/trofeo3");
        if (mvpSeasonSprite != null)
            _profileMvpSeasonIcon.style.backgroundImage = new StyleBackground(mvpSeasonSprite);
        _profileMvpSeasonCount.text = _player.season_mvps.ToString();

        var tex = PlayerPhotoHelper.Load(_player.id, _player.photo);
        _profilePhoto.style.backgroundImage = tex != null ? new StyleBackground(tex) : StyleKeyword.None;
    }

    /// <summary>Información personal: sin repetir posición/equipo (ya en el banner).</summary>
    void BuildPersonalInfo()
    {
        if (_profileInfoBody == null || _player == null) return;
        _profileInfoBody.Clear();

        AddInfoField(_profileInfoBody, "NACIONALIDAD", CountryCodes.GetName(_player.nationality), CountryCodes.GetFlagPath(_player.nationality));
        AddInfoRoleField(_profileInfoBody, "ROL", GetRoleName(_player.role), _player.role);
        AddInfoField(_profileInfoBody, "ALTURA", $"{_player.height_cm} cm");
        AddInfoField(_profileInfoBody, "PESO", $"{_player.weight_kg} kg");
    }

    /// <summary>Información contractual: salario, años, opciones y condición.</summary>
    void BuildContractInfo()
    {
        if (_profileContractBody == null || _player == null) return;
        _profileContractBody.Clear();

        string option = _player.has_team_option == 1 ? "Team Option"
                      : _player.has_player_option == 1 ? "Player Option"
                      : "—";

        AddInfoField(_profileContractBody, "SALARIO", $"${_player.salary:N0}");
        AddInfoField(_profileContractBody, "AÑOS DE CONTRATO", _player.contract_years.ToString());
        AddInfoField(_profileContractBody, "AÑOS GARANTIZADOS", _player.guaranteed_years.ToString());
        AddInfoField(_profileContractBody, "AÑOS DE SERVICIO", _player.years_of_service.ToString());
        AddInfoField(_profileContractBody, "AGENCIA LIBRE",
            _player.team_id == 0 && !string.IsNullOrEmpty(_player.free_agent_type)
                ? _player.free_agent_type
                : "—");
        AddInfoField(_profileContractBody, "OPCIÓN", option);
    }

    void AddInfoField(VisualElement target, string label, string value, string flagPath = null)
    {
        if (target == null) return;
        var field = new VisualElement();
        field.AddToClassList("playerprofile-info-field");

        var labLbl = new Label(label);
        labLbl.AddToClassList("playerprofile-info-label");
        field.Add(labLbl);

        var row = new VisualElement();
        row.AddToClassList("playerprofile-info-value-row");

        if (!string.IsNullOrEmpty(flagPath))
        {
            var flag = new VisualElement();
            flag.AddToClassList("playerprofile-info-flag");
            var flagTex = Resources.Load<Texture2D>(flagPath);
            if (flagTex != null)
                flag.style.backgroundImage = new StyleBackground(flagTex);
            row.Add(flag);
        }

        var valLbl = new Label(string.IsNullOrEmpty(value) ? "—" : value);
        valLbl.AddToClassList("playerprofile-info-value");
        row.Add(valLbl);

        field.Add(row);
        target.Add(field);
    }

    void AddInfoRoleField(VisualElement target, string label, string value, PlayerRole role)
    {
        if (target == null) return;
        var field = new VisualElement();
        field.AddToClassList("playerprofile-info-field");

        var labLbl = new Label(label);
        labLbl.AddToClassList("playerprofile-info-label");
        field.Add(labLbl);

        var row = new VisualElement();
        row.AddToClassList("playerprofile-info-value-row");

        string iconName = role switch
        {
            PlayerRole.Estrella => "rol_estrella",
            PlayerRole.Titular => "rol_titular",
            PlayerRole.Banquillo => "rol_banquillo",
            _ => "rol_ultimoRecurso"
        };
        var iconTex = Resources.Load<Texture2D>($"Icons/{iconName}");
        if (iconTex != null)
        {
            var icon = new VisualElement();
            icon.AddToClassList("playerprofile-info-role-icon");
            icon.style.backgroundImage = new StyleBackground(iconTex);
            row.Add(icon);
        }

        var valLbl = new Label(value);
        valLbl.AddToClassList("playerprofile-info-value");
        row.Add(valLbl);

        field.Add(row);
        target.Add(field);
    }

    /// <summary>Parte médico (E2): últimas lesiones cerradas del jugador.</summary>
    void BuildInjuryHistory()
    {
        if (_profileInjuries == null || _profileInjuriesBody == null || _player == null) return;
        _profileInjuriesBody.Clear();

        var history = DatabaseManager.Instance.GetInjuryHistory(_player.id, 6);
        if (history.Count == 0)
        {
            var empty = new Label("Sin lesiones registradas.");
            empty.AddToClassList("playerprofile-injury-empty");
            _profileInjuriesBody.Add(empty);
            return;
        }

        foreach (var h in history)
        {
            var row = new VisualElement();
            row.AddToClassList("playerprofile-injury-row");

            var dateLbl = new Label(FormatInjuryDate(h.date));
            dateLbl.AddToClassList("playerprofile-injury-date");
            row.Add(dateLbl);

            var typeLbl = new Label(string.IsNullOrEmpty(h.injury_type) ? "Lesión" : h.injury_type);
            typeLbl.AddToClassList("playerprofile-injury-type");
            row.Add(typeLbl);

            var daysLbl = new Label($"{h.days} d");
            daysLbl.AddToClassList("playerprofile-injury-days");
            row.Add(daysLbl);

            bool forced = h.outcome == "alta_forzada";
            var outcomeLbl = new Label(forced ? "ALTA FORZADA" : "RECUPERADO");
            outcomeLbl.AddToClassList("playerprofile-injury-outcome");
            if (forced) outcomeLbl.AddToClassList("playerprofile-injury-outcome--forced");
            row.Add(outcomeLbl);

            _profileInjuriesBody.Add(row);
        }
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

        AddStatCard("PTS", StatFormat.One(avgPts));
        AddStatCard("REB", StatFormat.One(avgReb));
        AddStatCard("AST", StatFormat.One(avgAst));
        AddStatCard("ROB", StatFormat.One(avgStl));
        AddStatCard("TAP", StatFormat.One(avgBlk));
        AddStatCard("TO", StatFormat.One(avgTov), StatFormat.TovTip);
        AddStatCard("MIN", StatFormat.One(avgMin), StatFormat.MinTip);
        AddStatCard("TC%", StatFormat.One(fgPct));
        AddStatCard("3P%", StatFormat.One(fg3Pct));
        AddStatCard("TL%", StatFormat.One(ftPct));
        AddStatCardAccent("EFG%", StatFormat.One(efgPct), StatFormat.EfgTip);
        AddStatCardAccent("TS%", StatFormat.One(tsPct), StatFormat.TsTip);
        AddStatCardAccent("PER", StatFormat.One(per), StatFormat.PerTip);
        AddStatCard("VAL", StatFormat.One((float)agg.total_rating / g), StatFormat.ValTip);
    }

    void AddStatCard(string label, string value, string tooltip = null)
    {
        var card = new VisualElement();
        card.AddToClassList("playerprofile-stat-card");
        if (!string.IsNullOrEmpty(tooltip)) card.tooltip = tooltip;

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

    void AddStatCardAccent(string label, string value, string tooltip = null)
    {
        var card = new VisualElement();
        card.AddToClassList("playerprofile-stat-card");
        if (!string.IsNullOrEmpty(tooltip)) card.tooltip = tooltip;

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
        // El bloqueo de fog-of-war se muestra dentro del panel de atributos: se oculta el
        // cuerpo (badges) y se muestra el aviso, no el panel entero.
        if (!canView)
        {
            _profileAttrsBody.style.display = DisplayStyle.None;
            _profileAttrsLocked.style.display = DisplayStyle.Flex;
            return;
        }

        _profileAttrsBody.style.display = DisplayStyle.Flex;
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
            // Los atributos se muestran siempre en 0-99 (la moral interna llega a 100).
            int shown = Mathf.Clamp(val, 0, 99);

            var badge = new VisualElement();
            badge.AddToClassList("playerprofile-attr-badge");
            // Rangos como en Roster/Quinteto: <40 rojo, <70 dorado, resto verde.
            if (shown < 40)
                badge.AddToClassList("playerprofile-attr-badge--low");
            else if (shown < 70)
                badge.AddToClassList("playerprofile-attr-badge--mid");

            var valLbl = new Label();
            valLbl.AddToClassList("playerprofile-attr-badge-value");
            valLbl.text = shown.ToString();

            var labLbl = new Label();
            labLbl.AddToClassList("playerprofile-attr-badge-label");
            labLbl.text = label;

            badge.Add(valLbl);
            badge.Add(labLbl);
            _profileAttrsBody.Add(badge);
        }
    }

    /// <summary>Fecha en formato dd/MM/yyyy (la BD guarda yyyy-MM-dd o dd/MM/yyyy).</summary>
    static string FormatInjuryDate(string date)
    {
        if (string.IsNullOrEmpty(date)) return "—";
        if (System.DateTime.TryParseExact(date, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var exact))
            return exact.ToString("dd/MM/yyyy");
        if (System.DateTime.TryParse(date, out var parsed))
            return parsed.ToString("dd/MM/yyyy");
        return date;
    }

    static string GetRoleName(PlayerRole role) => role switch
    {
        PlayerRole.Estrella => "Estrella",
        PlayerRole.Titular => "Titular",
        PlayerRole.Banquillo => "Banquillo",
        _ => "Último recurso"
    };
}
