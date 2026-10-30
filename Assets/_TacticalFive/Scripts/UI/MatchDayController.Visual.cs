using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Vista 2D del partido: reproduce la crónica ya simulada sobre una cancha estilizada.
// No vuelve a simular ni modifica el resultado; solo presenta los eventos y deltas
// que genera GameSimulator.
public partial class MatchDayController
{
    private const string SIM2D_SPEED_PREF = "TF_Sim2DSpeed";
    private const float SIM2D_COURT_ASPECT = 28f / 15f;
    // Centros de los aros en la imagen de la cancha (1400x750, margen 20 px).
    private const float SIM2D_LEFT_HOOP_X = 0.069f;
    private const float SIM2D_RIGHT_HOOP_X = 0.931f;
    private const float SIM2D_HOOP_Y = 0.5f;
    private const float SIM2D_LEFT_FT_X = 0.215f;
    private const float SIM2D_RIGHT_FT_X = 0.785f;
    private static readonly Color32[] Sim2DScoreParticleColors =
    {
        new Color32(255, 214, 90, 255),
        new Color32(255, 166, 61, 255),
        new Color32(255, 245, 200, 255)
    };

    private VisualElement _sim2DOverlay, _sim2DCourt, _sim2DHomeLayer, _sim2DAwayLayer, _sim2DBall;
    private VisualElement _sim2DEffectsLayer;
    private VisualElement _sim2DCourtTop, _sim2DHoopOwners;
    private VisualElement _sim2DHomeLogo, _sim2DAwayLogo, _sim2DTeamList;
    private VisualElement _sim2DLeftHoopLogo, _sim2DRightHoopLogo;
    private VisualElement _sim2DLeftAttackArrow, _sim2DRightAttackArrow;
    private Label _sim2DHomeAbbr, _sim2DAwayAbbr, _sim2DHomeName, _sim2DAwayName;
    private Label _sim2DScore, _sim2DQuarter, _sim2DClock, _sim2DTickerText;
    private Label _sim2DPossession, _sim2DVenue, _sim2DShotClockLabel;
    private VisualElement _sim2DTickerIcon;
    private Label _sim2DHeaderPlayer, _sim2DHeaderPts, _sim2DHeaderReb, _sim2DHeaderAsi;
    private Button _sim2DHomeTab, _sim2DAwayTab, _sim2DPause, _sim2DSkip;
    private Button _sim2DSpeed1, _sim2DSpeed5, _sim2DSpeed10, _sim2DSpeed20;

    private Coroutine _sim2DRoutine;
    private List<GameSimulator.PlayByPlayEvent> _sim2DLog;
    private readonly List<Coroutine> _sim2DScoreEffectRoutines = new();
    private bool _sim2DActive, _sim2DFinished, _sim2DPaused;
    private int _sim2DGameId, _sim2DHomeTeamId, _sim2DAwayTeamId, _sim2DSelectedTeamId;
    private int _sim2DSpeed = 1;
    private bool _sim2DHomeOnOffense = true;
    private List<int> _sim2DHomeOnCourt = new();
    private List<int> _sim2DAwayOnCourt = new();
    private float _sim2DBallX = 0.5f, _sim2DBallY = 0.5f;
    private float _sim2DBallTargetX = 0.5f, _sim2DBallTargetY = 0.5f;
    private int _sim2DBallHandlerId;
    private bool _sim2DBallFollowsHandler;
    private readonly List<Sim2DBallWaypoint> _sim2DBallPath = new();
    private readonly Dictionary<int, List<Sim2DPlayerWaypoint>> _sim2DPlayerPaths = new();
    private float _sim2DShotClock = 24f;
    private Sim2DInboundType _sim2DInboundType;
    private float _sim2DInboundY = 0.5f;

    private readonly Dictionary<int, Sim2DPlayerState> _sim2DPlayers = new();

    private class Sim2DPlayerState
    {
        public PlayerData Player;
        public int TeamId;
        public int Points, Rebounds, Assists;
        public Texture2D Photo;
        public VisualElement Node;
        public float X = 0.5f, Y = 0.5f;
        public float TargetX = 0.5f, TargetY = 0.5f;
        public bool OnCourt;
    }

    private class Sim2DScoreParticle
    {
        public VisualElement Element;
        public float X, Y, Vx, Vy;
    }

    private enum Sim2DInboundType { None, TipOff, Baseline, Sideline, DeadBall, Outlet }

    private class Sim2DBallWaypoint
    {
        public Vector2 Position;
        public float Time;
        public bool IsShot;
    }

    private class Sim2DPlayerWaypoint
    {
        public Vector2 Position;
        public float Time;
    }

    void CacheSimVisualReferences()
    {
        _sim2DOverlay = _root.Q<VisualElement>("SimVisualOverlay");
        if (_sim2DOverlay == null) return;

        _sim2DCourt = _root.Q<VisualElement>("Sim2DCourt");
        _sim2DCourtTop = _root.Q<VisualElement>("Sim2DCourtTop");
        _sim2DHoopOwners = _root.Q<VisualElement>("Sim2DHoopOwners");
        _sim2DHomeLayer = _root.Q<VisualElement>("Sim2DHomeLayer");
        _sim2DAwayLayer = _root.Q<VisualElement>("Sim2DAwayLayer");
        _sim2DBall = _root.Q<VisualElement>("Sim2DBall");
        _sim2DEffectsLayer = _root.Q<VisualElement>("Sim2DEffectsLayer");
        if (_sim2DEffectsLayer != null)
            _sim2DEffectsLayer.pickingMode = PickingMode.Ignore;
        if (_sim2DCourt != null)
        {
            var courtTexture = Resources.Load<Texture2D>("Courts/court_lines");
            if (courtTexture != null)
                _sim2DCourt.style.backgroundImage = new StyleBackground(courtTexture);
            _sim2DCourt.RegisterCallback<GeometryChangedEvent>(OnSim2DCourtGeometryChanged);
        }
        if (_sim2DBall != null)
        {
            var ballSprite = Resources.Load<Sprite>("Icons/pelota");
            if (ballSprite != null)
            {
                _sim2DBall.style.backgroundImage = new StyleBackground(ballSprite);
            }
            else
            {
                var ballTexture = Resources.Load<Texture2D>("Icons/pelota");
                if (ballTexture != null)
                    _sim2DBall.style.backgroundImage = new StyleBackground(ballTexture);
                else
                    _sim2DBall.style.backgroundColor = new StyleColor(new Color32(232, 128, 42, 255));
            }
        }
        _sim2DHomeLogo = _root.Q<VisualElement>("Sim2DHomeLogo");
        _sim2DAwayLogo = _root.Q<VisualElement>("Sim2DAwayLogo");
        _sim2DTeamList = _root.Q<VisualElement>("Sim2DTeamList");
        _sim2DLeftHoopLogo = _root.Q<VisualElement>("Sim2DLeftHoopLogo");
        _sim2DRightHoopLogo = _root.Q<VisualElement>("Sim2DRightHoopLogo");
        _sim2DLeftAttackArrow = _root.Q<VisualElement>("Sim2DLeftAttackArrow");
        _sim2DRightAttackArrow = _root.Q<VisualElement>("Sim2DRightAttackArrow");
        SetSim2DIcon(_sim2DLeftAttackArrow, "Icons/right_arrow");
        SetSim2DIcon(_sim2DRightAttackArrow, "Icons/left_arrow");

        _sim2DHomeAbbr = _root.Q<Label>("Sim2DHomeAbbr");
        _sim2DAwayAbbr = _root.Q<Label>("Sim2DAwayAbbr");
        _sim2DHomeName = _root.Q<Label>("Sim2DHomeName");
        _sim2DAwayName = _root.Q<Label>("Sim2DAwayName");
        _sim2DScore = _root.Q<Label>("Sim2DScore");
        _sim2DQuarter = _root.Q<Label>("Sim2DQuarter");
        _sim2DClock = _root.Q<Label>("Sim2DClock");
        _sim2DTickerText = _root.Q<Label>("Sim2DTickerText");
        _sim2DTickerIcon = _root.Q<VisualElement>("Sim2DTickerIcon");
        if (_sim2DTickerIcon != null)
        {
            var actionSprite = Resources.Load<Sprite>("Icons/accion");
            if (actionSprite != null)
            {
                _sim2DTickerIcon.style.backgroundImage = new StyleBackground(actionSprite);
            }
            else
            {
                var actionTexture = Resources.Load<Texture2D>("Icons/accion");
                if (actionTexture != null)
                    _sim2DTickerIcon.style.backgroundImage = new StyleBackground(actionTexture);
            }
        }
        _sim2DPossession = _root.Q<Label>("Sim2DPossession");
        _sim2DVenue = _root.Q<Label>("Sim2DVenue");
        _sim2DShotClockLabel = _root.Q<Label>("Sim2DShotClock");
        _sim2DHeaderPlayer = _root.Q<Label>("Sim2DHeaderPlayer");
        _sim2DHeaderPts = _root.Q<Label>("Sim2DHeaderPts");
        _sim2DHeaderReb = _root.Q<Label>("Sim2DHeaderReb");
        _sim2DHeaderAsi = _root.Q<Label>("Sim2DHeaderAsi");

        _sim2DHomeTab = _root.Q<Button>("Sim2DHomeTab");
        _sim2DAwayTab = _root.Q<Button>("Sim2DAwayTab");
        _sim2DPause = _root.Q<Button>("Sim2DPause");
        _sim2DSkip = _root.Q<Button>("Sim2DSkip");
        _sim2DSpeed1 = _root.Q<Button>("Sim2DSpeed1");
        _sim2DSpeed5 = _root.Q<Button>("Sim2DSpeed5");
        _sim2DSpeed10 = _root.Q<Button>("Sim2DSpeed10");
        _sim2DSpeed20 = _root.Q<Button>("Sim2DSpeed20");

        _sim2DSpeed = NormalizeSim2DSpeed(PlayerPrefs.GetInt(SIM2D_SPEED_PREF, 1));
        UpdateSim2DSpeedButtons();
    }

    void ConfigureSim2DHeader()
    {
        if (_sim2DHeaderPlayer == null) return;

        _sim2DHeaderPlayer.text = LocalizationService.T("ui.match_day.jugador");
        _sim2DHeaderPts.text = LocalizationService.T("ui.match_day.pts");
        _sim2DHeaderReb.text = LocalizationService.T("ui.match_day.reb");
        _sim2DHeaderAsi.text = LocalizationService.T("ui.match_day.asi");

        ApplySim2DHeaderStyle(_sim2DHeaderPlayer, TextAnchor.MiddleLeft, 0, true);
        ApplySim2DHeaderStyle(_sim2DHeaderPts, TextAnchor.MiddleCenter, 52, false);
        ApplySim2DHeaderStyle(_sim2DHeaderReb, TextAnchor.MiddleCenter, 52, false);
        ApplySim2DHeaderStyle(_sim2DHeaderAsi, TextAnchor.MiddleCenter, 52, false);
    }

    static void ApplySim2DHeaderStyle(Label label, TextAnchor alignment, float width, bool grow)
    {
        if (label == null) return;
        label.style.display = DisplayStyle.Flex;
        label.style.visibility = Visibility.Visible;
        label.style.height = 28;
        label.style.fontSize = 18;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = alignment;
        label.style.flexShrink = grow ? 1 : 0;
        if (grow)
        {
            // La cabecera usa las mismas columnas que las filas: foto (80+10),
            // posición (30+8), nombre flexible y estadísticas fijas a la derecha.
            label.style.marginLeft = 128;
            label.style.flexGrow = 1;
            label.style.flexShrink = 1;
            label.style.minWidth = 0;
        }
        else
        {
            label.style.width = width;
        }
        label.style.color = new StyleColor(grow
            ? new Color32(190, 200, 218, 255)
            : new Color32(212, 160, 23, 255));
    }

    static void SetSim2DIcon(VisualElement elem, string resource)
    {
        if (elem == null) return;
        var sprite = Resources.Load<Sprite>(resource);
        if (sprite != null)
        {
            elem.style.backgroundImage = new StyleBackground(sprite);
            return;
        }
        var texture = Resources.Load<Texture2D>(resource);
        if (texture != null)
            elem.style.backgroundImage = new StyleBackground(texture);
    }

    void RegisterSimVisualCallbacks()
    {
        if (_sim2DOverlay == null) return;

        _sim2DHomeTab?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _sim2DSelectedTeamId = _sim2DHomeTeamId;
            UpdateSim2DTabs();
            UpdateSim2DRoster();
        });
        _sim2DAwayTab?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _sim2DSelectedTeamId = _sim2DAwayTeamId;
            UpdateSim2DTabs();
            UpdateSim2DRoster();
        });
        _sim2DPause?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ToggleSim2DPause(); });
        _sim2DSkip?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnSim2DSkip(); });
        _sim2DSpeed1?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectSim2DSpeed(1); });
        _sim2DSpeed5?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectSim2DSpeed(5); });
        _sim2DSpeed10?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectSim2DSpeed(10); });
        _sim2DSpeed20?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectSim2DSpeed(20); });

        if (CursorManager.Instance == null) return;
        foreach (var name in new[]
        {
            "Sim2DHomeTab", "Sim2DAwayTab", "Sim2DPause", "Sim2DSkip",
            "Sim2DSpeed1", "Sim2DSpeed5", "Sim2DSpeed10", "Sim2DSpeed20"
        })
        {
            var el = _root.Q<VisualElement>(name);
            if (el != null) CursorManager.Instance.RegisterHandCursor(el);
        }
    }

    void BuildSimVisual(int gameId, List<GameSimulator.PlayByPlayEvent> log)
    {
        if (_sim2DOverlay == null) return;

        ClearSim2DScoreEffects();
        _sim2DGameId = gameId;
        _sim2DLog = log;
        _sim2DActive = true;
        _sim2DFinished = false;
        _sim2DPaused = false;
        _sim2DHomeOnOffense = true;
        _sim2DHomeOnCourt = new List<int>();
        _sim2DAwayOnCourt = new List<int>();
        _sim2DBallHandlerId = 0;
        _sim2DBallPath.Clear();
        _sim2DBallFollowsHandler = false;
        _sim2DInboundType = Sim2DInboundType.None;
        _sim2DShotClock = 24f;
        UpdateSim2DShotClock(_sim2DShotClock);

        var home = _allTeams?.Find(t => t.id == _sim2DHomeTeamId)
                   ?? DatabaseManager.Instance.GetTeamById(_sim2DHomeTeamId);
        var away = _allTeams?.Find(t => t.id == _sim2DAwayTeamId)
                   ?? DatabaseManager.Instance.GetTeamById(_sim2DAwayTeamId);
        _sim2DHomeTab.text = home?.abbreviation ?? "LOC";
        _sim2DAwayTab.text = away?.abbreviation ?? "VIS";
        _sim2DHomeAbbr.text = home?.name ?? "--";
        _sim2DAwayAbbr.text = away?.name ?? "--";
        _sim2DHomeName.style.display = DisplayStyle.None;
        _sim2DAwayName.style.display = DisplayStyle.None;
        _sim2DHomeName.text = "";
        _sim2DAwayName.text = "";
        SetLogo(_sim2DHomeLogo, home?.logo, "64x64");
        SetLogo(_sim2DAwayLogo, away?.logo, "64x64");
        // Sin cambio de lado: el local defiende el aro izquierdo (ataca a la derecha)
        // y el visitante defiende el derecho (ataca a la izquierda).
        SetLogo(_sim2DLeftHoopLogo, home?.logo, "64x64");
        SetLogo(_sim2DRightHoopLogo, away?.logo, "64x64");
        _sim2DVenue.text = home?.arena ?? "";
        ConfigureSim2DHeader();

        _sim2DSelectedTeamId = _myTeam != null &&
            (_myTeam.id == _sim2DHomeTeamId || _myTeam.id == _sim2DAwayTeamId)
            ? _myTeam.id
            : _sim2DHomeTeamId;

        if (_sim2DPause != null)
        {
            _sim2DPause.SetEnabled(true);
            _sim2DPause.text = LocalizationService.T("ui.code.match_day_controller.pausa");
        }
        if (_sim2DSkip != null)
            _sim2DSkip.text = LocalizationService.T("ui.code.match_day_controller.saltar");

        BuildSim2DPlayerStates();
        BuildSim2DInitialOnCourt();
        UpdateSim2DTabs();
        UpdateSim2DScoreboard(0, 0, 1, 0f);
        _sim2DTickerText.text = log.Count > 0
            ? PlayByPlayNarrator.Narrate(log[0].text, log[0].homeScore, log[0].awayScore,
                log[0].quarter, _pbpNarrator.NextDouble())
            : "";
        UpdateSim2DPossession();
        UpdateSim2DRoster();
        LayoutSim2DTargets(log.Count > 0 ? log[0] : null, 0, animate: false);

        _sim2DOverlay.style.display = DisplayStyle.Flex;
        if (_sim2DRoutine != null) StopCoroutine(_sim2DRoutine);
        _sim2DRoutine = StartCoroutine(Sim2DCoroutine(log));
    }

    void BuildSim2DPlayerStates()
    {
        _sim2DPlayers.Clear();
        if (_sim2DHomeLayer != null) _sim2DHomeLayer.Clear();
        if (_sim2DAwayLayer != null) _sim2DAwayLayer.Clear();

        var stats = DatabaseManager.Instance.GetGamePlayerStats(_sim2DGameId);
        foreach (var s in stats)
            AddSim2DPlayerState(s.player_id, s.team_id);

        var starters = GameResultCache.GameStarters.GetValueOrDefault(_sim2DGameId);
        if (starters != null)
        {
            foreach (int id in starters)
            {
                if (_sim2DPlayers.ContainsKey(id)) continue;
                var p = DatabaseManager.Instance.GetPlayerById(id);
                if (p == null) continue;
                AddSim2DPlayerState(id, p.team_id);
            }
        }
    }

    void AddSim2DPlayerState(int playerId, int teamId)
    {
        if (_sim2DPlayers.ContainsKey(playerId)) return;
        var player = DatabaseManager.Instance.GetPlayerById(playerId);
        if (player == null) return;

        var state = new Sim2DPlayerState
        {
            Player = player,
            TeamId = teamId,
            Photo = PlayerPhotoHelper.Load(player.id, player.photo)
        };
        state.Node = CreateSim2DPlayerNode(state);
        _sim2DPlayers[playerId] = state;
    }

    VisualElement CreateSim2DPlayerNode(Sim2DPlayerState state)
    {
        var node = new VisualElement();
        node.AddToClassList("sim2d-player");
        node.AddToClassList(state.TeamId == _sim2DHomeTeamId ? "sim2d-player--home" : "sim2d-player--away");

        var photo = new VisualElement();
        photo.AddToClassList("sim2d-player-photo");
        if (state.Photo != null)
            photo.style.backgroundImage = new StyleBackground(state.Photo);

        var plate = new VisualElement();
        plate.AddToClassList("sim2d-player-plate");
        var number = new Label { text = state.Player.number > 0 ? state.Player.number.ToString() : "" };
        number.AddToClassList("sim2d-player-number");
        var name = new Label { text = ShortName(state.Player) };
        name.AddToClassList("sim2d-player-name");
        plate.Add(number);
        plate.Add(name);
        plate.style.display = DisplayStyle.Flex;
        plate.style.visibility = Visibility.Visible;
        number.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        name.style.color = new StyleColor(new Color32(235, 239, 247, 255));
        name.style.display = DisplayStyle.Flex;
        name.style.visibility = Visibility.Visible;

        node.Add(photo);
        node.Add(plate);

        var layer = state.TeamId == _sim2DHomeTeamId ? _sim2DHomeLayer : _sim2DAwayLayer;
        layer?.Add(node);
        node.style.display = DisplayStyle.None;
        return node;
    }

    string Sim2DEventText(GameSimulator.PlayByPlayEvent ev)
    {
        if (ev == null) return "";
        int stealId = FindSim2DPlayerForStat(ev, "stl");
        if (stealId > 0 && _sim2DPlayers.TryGetValue(stealId, out var thief))
            return $"{thief.Player.first_name} {thief.Player.last_name} roba el balón. {ev.text}";

        return PlayByPlayNarrator.Narrate(
            ev.text, ev.homeScore, ev.awayScore, ev.quarter, _pbpNarrator.NextDouble());
    }

    static string FullName(PlayerData p)
    {
        if (p == null) return "";
        string full = $"{p.first_name} {p.last_name}".Trim();
        return string.IsNullOrWhiteSpace(full) ? $"#{p.id}" : full;
    }

    static string ShortName(PlayerData p)
    {
        if (p == null) return "";
        string last = string.IsNullOrWhiteSpace(p.last_name) ? p.first_name : p.last_name;
        if (string.IsNullOrWhiteSpace(last)) last = $"#{p.id}";
        return last.ToUpperInvariant();
    }

    static int Sim2DPositionOrder(string position) => position switch
    {
        "PG" => 0,
        "SG" => 1,
        "SF" => 2,
        "PF" => 3,
        "C" => 4,
        _ => 5
    };

    void BuildSim2DInitialOnCourt()
    {
        var starters = GameResultCache.GameStarters.GetValueOrDefault(_sim2DGameId)
                       ?? new HashSet<int>();
        _sim2DHomeOnCourt = BuildSim2DOnCourt(_sim2DHomeTeamId, starters);
        _sim2DAwayOnCourt = BuildSim2DOnCourt(_sim2DAwayTeamId, starters);
    }

    List<int> BuildSim2DOnCourt(int teamId, HashSet<int> starters)
    {
        var candidates = _sim2DPlayers.Values
            .Where(s => s.TeamId == teamId)
            .OrderBy(s => Sim2DPositionOrder(s.Player.position))
            .ThenByDescending(s => s.Player.overall)
            .Select(s => s.Player.id)
            .ToList();

        var result = candidates.Where(starters.Contains).Take(5).ToList();
        foreach (int id in candidates)
        {
            if (result.Count >= 5) break;
            if (!result.Contains(id)) result.Add(id);
        }
        return result;
    }

    List<int> OrderSim2DOnCourt(List<int> ids)
    {
        if (ids == null) return new List<int>();
        return ids
            .Where(id => _sim2DPlayers.ContainsKey(id))
            .OrderBy(id => Sim2DPositionOrder(_sim2DPlayers[id].Player.position))
            .ThenByDescending(id => _sim2DPlayers[id].Player.overall)
            .ToList();
    }

    void UpdateSim2DTabs()
    {
        _sim2DHomeTab?.EnableInClassList("sim2d-tab--active", _sim2DSelectedTeamId == _sim2DHomeTeamId);
        _sim2DAwayTab?.EnableInClassList("sim2d-tab--active", _sim2DSelectedTeamId == _sim2DAwayTeamId);
    }

    void UpdateSim2DRoster()
    {
        if (_sim2DTeamList == null) return;
        _sim2DTeamList.Clear();

        var ids = _sim2DSelectedTeamId == _sim2DHomeTeamId
            ? _sim2DHomeOnCourt
            : _sim2DAwayOnCourt;
        ids = OrderSim2DOnCourt(ids);
        if (ids.Count == 0)
        {
            ids = _sim2DPlayers.Values
                .Where(s => s.TeamId == _sim2DSelectedTeamId)
                .OrderBy(s => Sim2DPositionOrder(s.Player.position))
                .Take(5)
                .Select(s => s.Player.id)
                .ToList();
        }

        foreach (int id in ids)
        {
            if (!_sim2DPlayers.TryGetValue(id, out var state)) continue;

            var row = new VisualElement();
            row.AddToClassList("sim2d-roster-row");
            row.AddToClassList("sim2d-roster-row--active");

            var photo = new VisualElement();
            photo.AddToClassList("sim2d-roster-photo");
            if (state.Photo != null)
                photo.style.backgroundImage = new StyleBackground(state.Photo);
            row.Add(photo);

            var pos = new Label { text = PositionCodes.GetShort(state.Player.position) };
            pos.AddToClassList("sim2d-roster-pos");
            pos.style.color = new StyleColor(new Color32(212, 160, 23, 255));
            pos.style.display = DisplayStyle.Flex;
            pos.style.visibility = Visibility.Visible;
            row.Add(pos);

            var name = new Label
            {
                text = FullName(state.Player),
                tooltip = FullName(state.Player)
            };
            name.AddToClassList("sim2d-roster-name");
            name.style.flexGrow = 1;
            name.style.flexShrink = 1;
            name.style.minWidth = 0;
            name.style.height = 80;
            name.style.fontSize = 24;
            name.style.color = new StyleColor(new Color32(235, 239, 247, 255));
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            name.style.display = DisplayStyle.Flex;
            name.style.visibility = Visibility.Visible;
            row.Add(name);

            var pts = new Label { text = state.Points.ToString() };
            pts.AddToClassList("sim2d-roster-stat");
            row.Add(pts);

            var reb = new Label { text = state.Rebounds.ToString() };
            reb.AddToClassList("sim2d-roster-stat");
            row.Add(reb);

            var ast = new Label { text = state.Assists.ToString() };
            ast.AddToClassList("sim2d-roster-stat");
            row.Add(ast);

            _sim2DTeamList.Add(row);
        }
    }

    void ApplySim2DDeltas(List<GameSimulator.StatDelta> deltas)
    {
        if (deltas == null) return;
        foreach (var d in deltas)
        {
            if (!_sim2DPlayers.TryGetValue(d.player_id, out var state)) continue;
            switch (d.stat)
            {
                case "pts": state.Points += (int)d.amount; break;
                case "oreb":
                case "dreb": state.Rebounds += (int)d.amount; break;
                case "ast": state.Assists += (int)d.amount; break;
            }
        }
    }

    void UpdateSim2DScoreboard(int home, int away, int quarter, float elapsed)
    {
        _sim2DScore.text = $"{home} - {away}";
        _sim2DQuarter.text = quarter > 4 ? "PRÓRROGA" : $"CUARTO {quarter}";
        float quarterLength = quarter <= 4 ? 12f : 5f;
        float remaining = Mathf.Max(0f, quarterLength - elapsed);
        int mm = Mathf.FloorToInt(remaining);
        int ss = Mathf.RoundToInt((remaining - mm) * 60f);
        if (ss >= 60) { mm++; ss = 0; }
        _sim2DClock.text = $"{mm:D2}:{ss:D2}";
    }

    void UpdateSim2DPossession()
    {
        if (_sim2DPossession == null) return;
        int teamId = _sim2DHomeOnOffense ? _sim2DHomeTeamId : _sim2DAwayTeamId;
        var team = _allTeams?.Find(t => t.id == teamId)
                   ?? DatabaseManager.Instance.GetTeamById(teamId);
        _sim2DPossession.text = LocalizationService.T(
            "ui.code.match_day_controller.sim_ataque", team?.name ?? "");
    }

    IEnumerator Sim2DCoroutine(List<GameSimulator.PlayByPlayEvent> log)
    {
        GameSimulator.PlayByPlayEvent previousAction = null;
        for (int i = 0; i < log.Count; i++)
        {
            while (_sim2DPaused) yield return null;
            var ev = log[i];
            ApplySim2DDeltas(ev.deltas);
            bool hasLineups = ev.homeOnCourt != null && ev.homeOnCourt.Count > 0;
            if (hasLineups)
            {
                _sim2DHomeOnCourt = new List<int>(ev.homeOnCourt);
                _sim2DAwayOnCourt = new List<int>(ev.awayOnCourt ?? new List<int>());
                _sim2DHomeOnOffense = ev.homeOnOffense;
                PrepareSim2DPossession(previousAction, ev);
            }

            UpdateSim2DScoreboard(ev.homeScore, ev.awayScore, ev.quarter, ev.timeElapsed);
            _sim2DTickerText.text = Sim2DEventText(ev);
            UpdateSim2DPossession();
            UpdateSim2DRoster();
            LayoutSim2DTargets(ev, i, animate: true);

            float currentAbsolute = GetSim2DAbsoluteMinutes(ev);
            var next = i + 1 < log.Count ? log[i + 1] : null;
            float clockTarget = GetSim2DClockTarget(ev, next);
            float nextAbsolute = next != null ? GetSim2DAbsoluteMinutes(next) : currentAbsolute;
            float gameSeconds = Mathf.Max(0f, (nextAbsolute - currentAbsolute) * 60f);
            float eventDuration = Mathf.Max(2.5f / Mathf.Max(1, _sim2DSpeed),
                gameSeconds / Mathf.Max(1, _sim2DSpeed));
            float shotClockStart = _sim2DShotClock;
            float shotClockEnd = Mathf.Max(0.1f, shotClockStart - gameSeconds);
            yield return AnimateSim2DPositions(ev.quarter, ev.timeElapsed, clockTarget,
                eventDuration, shotClockStart, shotClockEnd);

            // El aro se enciende cuando el balón ya ha llegado a la canasta.
            if (Sim2DIsFieldGoalMade(ev))
                PlaySim2DScoreEffect(ev.homeOnOffense, Sim2DIsThreePointer(ev));
            else if (Sim2DIsMadeFreeThrow(ev))
                PlaySim2DScoreEffect(ev.homeOnOffense, false, true);

            if (Sim2DIsOffensiveRebound(ev))
            {
                _sim2DShotClock = 14f;
                UpdateSim2DShotClock(_sim2DShotClock);
            }

            if (hasLineups)
                previousAction = ev;
        }
        OnSim2DComplete();
    }

    void PrepareSim2DPossession(GameSimulator.PlayByPlayEvent previous,
                                GameSimulator.PlayByPlayEvent current)
    {
        _sim2DInboundType = Sim2DInboundType.None;
        bool newPeriod = previous != null && previous.quarter != current.quarter;
        bool newPossession = previous == null || newPeriod
            || previous.homeOnOffense != current.homeOnOffense;
        if (newPossession)
        {
            _sim2DShotClock = 24f;
            if (previous == null && current.quarter == 1)
            {
                // El motor asigna el primer saque al local; la vista representa
                // el salto inicial sin volver a decidir ni cambiar el resultado.
                _sim2DInboundType = Sim2DInboundType.TipOff;
                _sim2DBallHandlerId = FindSim2DPointGuardForCurrentOffense();
            }
            else if (previous != null && !newPeriod)
            {
                int rebounderId = FindSim2DPlayerForStat(previous, "oreb", "dreb");
                int stealerId = FindSim2DPlayerForStat(previous, "stl");
                if (rebounderId > 0 && IsSim2DPlayerOnOffense(rebounderId))
                {
                    _sim2DBallHandlerId = rebounderId;
                    if (FindSim2DPlayerForStat(previous, "dreb") == rebounderId)
                        _sim2DInboundType = Sim2DInboundType.Outlet;
                }
                else if (stealerId > 0 && IsSim2DPlayerOnOffense(stealerId))
                    _sim2DBallHandlerId = stealerId;
                else if (Sim2DIsMadeBasket(previous) || Sim2DIsMadeFreeThrow(previous))
                {
                    _sim2DInboundType = Sim2DInboundType.Baseline;
                    _sim2DBallHandlerId = FindSim2DPointGuardForCurrentOffense();
                }
                else if (FindSim2DPlayerForStat(previous, "to") > 0)
                {
                    bool sideline = previous.turnoverType == "sideline";
                    _sim2DInboundType = sideline ? Sim2DInboundType.Sideline : Sim2DInboundType.DeadBall;
                    _sim2DInboundY = sideline
                        ? (_sim2DBallY < 0.5f ? 0.09f : 0.91f)
                        : Mathf.Clamp(_sim2DBallY, 0.12f, 0.88f);
                    _sim2DBallHandlerId = FindSim2DPointGuardForCurrentOffense();
                }
                else
                    _sim2DBallHandlerId = FindSim2DPointGuardForCurrentOffense();
            }
            else
                _sim2DBallHandlerId = FindSim2DPointGuardForCurrentOffense();
        }
        else if (Sim2DIsOffensiveRebound(previous))
        {
            _sim2DShotClock = 14f;
            int rebounderId = FindSim2DPlayerForStat(previous, "oreb");
            if (rebounderId > 0) _sim2DBallHandlerId = rebounderId;
        }

        UpdateSim2DShotClock(_sim2DShotClock);
    }

    int FindSim2DPointGuardForCurrentOffense()
    {
        var ids = _sim2DHomeOnOffense ? _sim2DHomeOnCourt : _sim2DAwayOnCourt;
        return OrderSim2DOnCourt(ids)
            .OrderBy(id => _sim2DPlayers[id].Player.position == "PG" ? 0
                : _sim2DPlayers[id].Player.position == "SG" ? 1
                : _sim2DPlayers[id].Player.position == "SF" ? 2 : 3)
            .ThenByDescending(id => _sim2DPlayers[id].Player.dribbling + _sim2DPlayers[id].Player.passing)
            .FirstOrDefault();
    }

    bool IsSim2DPlayerOnOffense(int playerId)
    {
        if (!_sim2DPlayers.TryGetValue(playerId, out var state)) return false;
        int offenseTeamId = _sim2DHomeOnOffense ? _sim2DHomeTeamId : _sim2DAwayTeamId;
        return state.TeamId == offenseTeamId;
    }

    static bool Sim2DIsMadeBasket(GameSimulator.PlayByPlayEvent ev) =>
        ev?.deltas != null && ev.deltas.Any(d => d.stat == "fgm" && d.amount > 0);

    static bool Sim2DIsMadeFreeThrow(GameSimulator.PlayByPlayEvent ev) =>
        ev?.deltas != null && ev.deltas.Any(d => d.stat == "ftm" && d.amount > 0);

    static bool Sim2DIsOffensiveRebound(GameSimulator.PlayByPlayEvent ev) =>
        ev?.deltas != null && ev.deltas.Any(d => d.stat == "oreb" && d.amount > 0);

    static float GetSim2DAbsoluteMinutes(GameSimulator.PlayByPlayEvent ev)
    {
        if (ev == null) return 0f;
        return ev.quarter <= 4
            ? (ev.quarter - 1) * 12f + ev.timeElapsed
            : 48f + (ev.quarter - 5) * 5f + ev.timeElapsed;
    }

    // Mantiene la proporción real 28:15 de la cancha (FIBA) para que aros,
    // círculos y triples no se vean ovalados al estirar la imagen.
    void OnSim2DCourtGeometryChanged(GeometryChangedEvent evt)
    {
        if (_sim2DCourt == null) return;
        var wrap = _sim2DCourt.parent;
        if (wrap == null) return;
        float width = wrap.resolvedStyle.width;
        float height = wrap.resolvedStyle.height;
        if (width < 10f || height < 10f) return;

        float reservedHeight = Mathf.Max(28f, _sim2DCourtTop?.resolvedStyle.height ?? 0f)
            + Mathf.Max(64f, _sim2DHoopOwners?.resolvedStyle.height ?? 0f) + 14f;
        float availableCourtHeight = Mathf.Max(10f, height - reservedHeight);
        float courtHeight = Mathf.Min(width / SIM2D_COURT_ASPECT, availableCourtHeight);
        float courtWidth = courtHeight * SIM2D_COURT_ASPECT;
        if (Mathf.Abs(_sim2DCourt.resolvedStyle.height - courtHeight) < 0.5f
            && Mathf.Abs(_sim2DCourt.resolvedStyle.width - courtWidth) < 0.5f) return;

        _sim2DCourt.style.flexGrow = 0;
        _sim2DCourt.style.flexShrink = 0;
        // Respeta la proporción y reserva sitio para la cabecera y los logos,
        // incluso cuando la ventana es más baja que 1080 px.
        _sim2DCourt.style.flexBasis = courtHeight;
        _sim2DCourt.style.width = courtWidth;
        _sim2DCourt.style.height = courtHeight;
        _sim2DCourt.style.alignSelf = Align.Center;
    }

    void LayoutSim2DTargets(GameSimulator.PlayByPlayEvent ev, int eventIndex, bool animate)
    {
        foreach (var state in _sim2DPlayers.Values)
        {
            state.OnCourt = false;
            state.TargetX = state.X;
            state.TargetY = state.Y;
            state.Node?.RemoveFromClassList("sim2d-player--action");
            state.Node?.RemoveFromClassList("sim2d-player--pressure");
            state.Node?.RemoveFromClassList("sim2d-player--inbound");
        }

        LayoutSim2DTeam(_sim2DHomeOnCourt, true, eventIndex);
        LayoutSim2DTeam(_sim2DAwayOnCourt, false, eventIndex);

        foreach (var state in _sim2DPlayers.Values)
        {
            if (state.Node == null) continue;
            state.Node.style.display = state.OnCourt ? DisplayStyle.Flex : DisplayStyle.None;
        }

        _sim2DBallTargetX = _sim2DBallX;
        _sim2DBallTargetY = _sim2DBallY;
        _sim2DBallPath.Clear();
        _sim2DPlayerPaths.Clear();
        _sim2DBallFollowsHandler = false;
        ApplySim2DActionTargets(ev, eventIndex);

        if (!animate)
        {
            foreach (var state in _sim2DPlayers.Values)
            {
                if (!state.OnCourt) continue;
                state.X = state.TargetX;
                state.Y = state.TargetY;
                ApplySim2DNodePosition(state);
            }
            _sim2DBallX = _sim2DBallTargetX;
            _sim2DBallY = _sim2DBallTargetY;
            ApplySim2DBallPosition();
        }
    }

    void LayoutSim2DTeam(List<int> ids, bool homeTeam, int eventIndex)
    {
        ids = OrderSim2DOnCourt(ids);
        bool offense = homeTeam == _sim2DHomeOnOffense;
        bool attackRight = _sim2DHomeOnOffense;
        var opponents = OrderSim2DOnCourt(homeTeam ? _sim2DAwayOnCourt : _sim2DHomeOnCourt);
        for (int i = 0; i < ids.Count; i++)
        {
            if (!_sim2DPlayers.TryGetValue(ids[i], out var state)) continue;
            state.OnCourt = true;
            Vector2 spot = GetSim2DPosition(state.Player.position, offense, attackRight);
            if (!offense)
            {
                // Defensa individual: cada defensor coge al rival de su posición y
                // acorta el espacio hacia él; el pívot conserva algo más de protección interior.
                var matchup = opponents.FirstOrDefault(id =>
                    _sim2DPlayers[id].Player.position == state.Player.position);
                if (matchup > 0 && _sim2DPlayers.TryGetValue(matchup, out var attacker))
                {
                    Vector2 attackerSpot = GetSim2DPosition(attacker.Player.position, true, attackRight);
                    spot = Vector2.Lerp(spot, attackerSpot,
                        state.Player.position == "C" ? 0.38f : 0.62f);
                }
            }

            // El desplazamiento tiene fase, amplitud y trayectoria distintas por jugador:
            // PG organiza, exteriores abren/cortan e interiores trabajan cerca del aro.
            float phase = eventIndex * 0.32f + state.Player.id * 0.113f;
            float amplitude = state.Player.position switch
            {
                "PG" => 0.014f,
                "SG" or "SF" => 0.022f,
                "PF" => 0.016f,
                "C" => 0.010f,
                _ => 0.015f
            };
            spot.x += Mathf.Sin(phase) * amplitude;
            spot.y += Mathf.Cos(phase * 1.31f) * amplitude * 1.65f;
            state.TargetX = Mathf.Clamp(spot.x, 0.04f, 0.96f);
            state.TargetY = Mathf.Clamp(spot.y, 0.08f, 0.92f);
        }
    }

    static Vector2 GetSim2DPosition(string position, bool offense, bool attackRight)
    {
        Vector2 spot = position switch
        {
            // El base organiza arriba; los escoltas/alero abren las alas y los interiores
            // ocupan codo, poste bajo y aro. Es una formación ilustrativa, no una jugada.
            "PG" => new Vector2(offense ? 0.58f : 0.52f, 0.50f),
            "SG" => new Vector2(offense ? 0.70f : 0.64f, 0.21f),
            "SF" => new Vector2(offense ? 0.70f : 0.64f, 0.79f),
            "PF" => new Vector2(offense ? 0.82f : 0.76f, 0.31f),
            "C"  => new Vector2(offense ? 0.84f : 0.79f, 0.69f),
            _    => new Vector2(offense ? 0.66f : 0.60f, 0.5f)
        };
        if (!attackRight) spot.x = 1f - spot.x;
        return spot;
    }

    void ApplySim2DActionTargets(GameSimulator.PlayByPlayEvent ev, int eventIndex)
    {
        if (ev?.deltas == null) return;

        int stealId = FindSim2DPlayerForStat(ev, "stl");
        int blockId = FindSim2DPlayerForStat(ev, "blk");
        int reboundId = FindSim2DPlayerForStat(ev, "oreb", "dreb");
        int turnoverId = FindSim2DPlayerForStat(ev, "to");
        int shooterId = FindSim2DPlayerForStat(ev, "fga", "fg3a");
        bool shotAttempt = ev.deltas.Any(d => (d.stat == "fga" || d.stat == "fg3a") && d.amount > 0);
        bool threePointAttempt = ev.deltas.Any(d => d.stat == "fg3a" && d.amount > 0);
        bool madeShot = ev.deltas.Any(d => d.stat == "fgm" && d.amount > 0);
        bool freeThrow = ev.deltas.Any(d => d.stat == "fta" && d.amount > 0);
        bool attackRight = _sim2DHomeOnOffense;
        var offenseIds = OrderSim2DOnCourt(_sim2DHomeOnOffense ? _sim2DHomeOnCourt : _sim2DAwayOnCourt);
        var defenseIds = OrderSim2DOnCourt(_sim2DHomeOnOffense ? _sim2DAwayOnCourt : _sim2DHomeOnCourt);

        if (!_sim2DPlayers.TryGetValue(_sim2DBallHandlerId, out var ballHandler)
            || !ballHandler.OnCourt || !offenseIds.Contains(_sim2DBallHandlerId))
        {
            _sim2DBallHandlerId = FindSim2DPointGuardForCurrentOffense();
            _sim2DPlayers.TryGetValue(_sim2DBallHandlerId, out ballHandler);
        }

        BeginSim2DBallPath();

        if (_sim2DInboundType != Sim2DInboundType.None)
        {
            SetupSim2DInbound(offenseIds, attackRight);
            if (_sim2DBallHandlerId > 0)
                _sim2DPlayers.TryGetValue(_sim2DBallHandlerId, out ballHandler);
        }

        // Una visita a la línea de libres cambia por completo la formación: tirador
        // en la línea, dos jugadores de cada equipo por cada lado para el rebote.
        if (freeThrow)
        {
            int ftShooterId = FindSim2DPlayerForStat(ev, "fta");
            if (ftShooterId <= 0) ftShooterId = shooterId > 0 ? shooterId : _sim2DBallHandlerId;
            LayoutSim2DFreeThrow(ftShooterId, offenseIds, defenseIds, attackRight);

            if (_sim2DPlayers.TryGetValue(ftShooterId, out var ftShooter))
            {
                AddSim2DBallWaypoint(new Vector2(ftShooter.TargetX, ftShooter.TargetY), 0.52f);
                int ftRebounderId = FindSim2DPlayerForStat(ev, "oreb", "dreb");
                if (ftRebounderId > 0 && _sim2DPlayers.TryGetValue(ftRebounderId, out var ftRebounder))
                {
                    float hoopX = attackRight ? SIM2D_RIGHT_HOOP_X : SIM2D_LEFT_HOOP_X;
                    AddSim2DBallWaypoint(new Vector2(hoopX, SIM2D_HOOP_Y), 0.82f, isShot: true);
                    SetSim2DTarget(ftRebounder, GetSim2DReboundPosition(ftRebounder, attackRight));
                    AddSim2DBallWaypoint(new Vector2(ftRebounder.TargetX, ftRebounder.TargetY), 0.98f);
                    _sim2DBallHandlerId = ftRebounderId;
                    _sim2DBallFollowsHandler = true;
                }
                else
                {
                    float hoopX = attackRight ? SIM2D_RIGHT_HOOP_X : SIM2D_LEFT_HOOP_X;
                    AddSim2DBallWaypoint(new Vector2(hoopX, SIM2D_HOOP_Y), 0.98f, isShot: true);
                    _sim2DBallHandlerId = 0;
                }
            }
            FinishSim2DBallPath();
            return;
        }

        if (shotAttempt && shooterId > 0 && _sim2DPlayers.TryGetValue(shooterId, out var shooter))
        {
            bool afterInbound = _sim2DInboundType != Sim2DInboundType.None;
            float passTime = afterInbound ? 0.86f : 0.52f;
            float shooterTime = afterInbound ? 0.92f : 0.70f;
            float shotContactTime = afterInbound ? 0.97f : 0.82f;
            Vector2 shotSpot = GetSim2DShotPosition(threePointAttempt, attackRight, eventIndex, shooter.Player.position);
            SetSim2DFormationTarget(shooter, shotSpot, shooterTime);
            shooter.Node?.EnableInClassList("sim2d-player--action", true);

            int defenderId = blockId > 0 ? blockId : FindNearestSim2DDefender(shooterId);
            if (defenderId > 0 && _sim2DPlayers.TryGetValue(defenderId, out var defender))
            {
                defender.TargetX = Mathf.Lerp(defender.TargetX, shooter.TargetX, blockId > 0 ? 0.88f : 0.56f);
                defender.TargetY = Mathf.Lerp(defender.TargetY, shooter.TargetY, blockId > 0 ? 0.88f : 0.56f);
                // La defensa llega a puntear todos los tiros; solo el delta real blk
                // enciende el efecto de tapón y desvía la trayectoria del balón.
                defender.Node?.EnableInClassList("sim2d-player--action", blockId > 0);
            }

            if (afterInbound && _sim2DInboundType != Sim2DInboundType.TipOff)
            {
                // El punto representa al propio base al terminar de subir el balón,
                // no una coordenada intermedia inventada en la pista.
                if (ballHandler != null)
                    AddSim2DBallWaypoint(new Vector2(ballHandler.TargetX, ballHandler.TargetY), 0.78f);
            }
            else if (ballHandler != null && _sim2DBallHandlerId != shooterId)
                AddSim2DBallWaypoint(new Vector2(ballHandler.TargetX, ballHandler.TargetY), 0.24f);

            int assistId = FindSim2DPlayerForStat(ev, "ast");
            if (assistId > 0 && assistId != shooterId
                && _sim2DPlayers.TryGetValue(assistId, out var passer) && passer.OnCourt)
            {
                if (assistId != _sim2DBallHandlerId)
                    AddSim2DBallWaypoint(new Vector2(passer.TargetX, passer.TargetY), passTime);
                AddSim2DBallWaypoint(new Vector2(shooter.TargetX, shooter.TargetY), shooterTime);
            }
            else
                AddSim2DBallWaypoint(new Vector2(shooter.TargetX, shooter.TargetY), shooterTime);

            float hoopTargetX = attackRight ? SIM2D_RIGHT_HOOP_X : SIM2D_LEFT_HOOP_X;
            var hoop = new Vector2(hoopTargetX, SIM2D_HOOP_Y);
            if (reboundId > 0 && _sim2DPlayers.TryGetValue(reboundId, out var rebounder))
            {
                AddSim2DBallWaypoint(hoop, shotContactTime, isShot: true);
                SetSim2DFormationTarget(rebounder, GetSim2DReboundPosition(rebounder, attackRight), 0.88f);
                rebounder.Node?.EnableInClassList("sim2d-player--action", true);
                AddSim2DBallWaypoint(new Vector2(rebounder.TargetX, rebounder.TargetY), 0.98f);
                _sim2DBallHandlerId = reboundId;
                _sim2DBallFollowsHandler = true;
            }
            else if (blockId > 0 && _sim2DPlayers.TryGetValue(blockId, out var blocker))
            {
                AddSim2DBallWaypoint(new Vector2(blocker.TargetX, blocker.TargetY),
                    afterInbound ? 0.985f : 0.94f);
                blocker.Node?.EnableInClassList("sim2d-player--action", true);
                _sim2DBallHandlerId = blockId;
                _sim2DBallFollowsHandler = true;
            }
            else if (madeShot)
            {
                AddSim2DBallWaypoint(hoop, 0.99f, isShot: true);
                _sim2DBallHandlerId = 0;
            }
            else
            {
                AddSim2DBallWaypoint(hoop, 0.96f, isShot: true);
                _sim2DBallHandlerId = 0;
            }

            FinishSim2DBallPath();
            ApplySim2DDefensivePressure(eventIndex, shooterId);
            return;
        }

        if (stealId > 0 && _sim2DPlayers.TryGetValue(stealId, out var thief))
        {
            if (turnoverId > 0 && _sim2DPlayers.TryGetValue(turnoverId, out var victim))
            {
                Vector2 victimPoint = new Vector2(victim.TargetX, victim.TargetY);
                thief.TargetX = Mathf.Lerp(thief.TargetX, victimPoint.x, 0.72f);
                thief.TargetY = Mathf.Lerp(thief.TargetY, victimPoint.y, 0.72f);
                victim.Node?.EnableInClassList("sim2d-player--action", true);
            }
            AddSim2DBallWaypoint(new Vector2(thief.TargetX, thief.TargetY), 0.48f);
            _sim2DBallHandlerId = stealId;
            _sim2DBallFollowsHandler = true;
            thief.Node?.EnableInClassList("sim2d-player--action", true);
        }
        else if (blockId > 0 && _sim2DPlayers.TryGetValue(blockId, out var blocker))
        {
            AddSim2DBallWaypoint(new Vector2(blocker.TargetX, blocker.TargetY), 0.50f);
            _sim2DBallHandlerId = blockId;
            _sim2DBallFollowsHandler = true;
            blocker.Node?.EnableInClassList("sim2d-player--action", true);
        }
        else if (reboundId > 0 && _sim2DPlayers.TryGetValue(reboundId, out var rebounder))
        {
            SetSim2DFormationTarget(rebounder, GetSim2DReboundPosition(rebounder, attackRight), 0.48f);
            AddSim2DBallWaypoint(new Vector2(rebounder.TargetX, rebounder.TargetY), 0.52f);
            _sim2DBallHandlerId = reboundId;
            _sim2DBallFollowsHandler = true;
            rebounder.Node?.EnableInClassList("sim2d-player--action", true);
        }
        else if (turnoverId > 0 && _sim2DPlayers.TryGetValue(turnoverId, out var offender))
        {
            bool sideline = ev.turnoverType == "sideline";
            _sim2DInboundY = sideline
                ? (offender.TargetY < 0.5f ? 0.09f : 0.91f)
                : Mathf.Clamp(offender.TargetY, 0.12f, 0.88f);
            Vector2 deadBall = sideline
                ? new Vector2(offender.TargetX, _sim2DInboundY)
                : new Vector2(offender.TargetX, offender.TargetY);
            AddSim2DBallWaypoint(deadBall, 0.58f);
            offender.Node?.EnableInClassList("sim2d-player--action", true);
            _sim2DBallHandlerId = 0;
        }
        else
        {
            if (ballHandler != null)
            {
                if (_sim2DInboundType == Sim2DInboundType.None)
                {
                    float direction = attackRight ? 1f : -1f;
                    ballHandler.TargetX = Mathf.Clamp(ballHandler.TargetX + direction * 0.035f, 0.05f, 0.95f);
                    AddSim2DBallWaypoint(new Vector2(ballHandler.TargetX, ballHandler.TargetY), 0.30f);
                }
                _sim2DBallHandlerId = ballHandler.Player.id;
                _sim2DBallFollowsHandler = true;
            }
        }

        if (stealId == 0 && blockId == 0)
            ApplySim2DDefensivePressure(eventIndex, turnoverId > 0 ? turnoverId : _sim2DBallHandlerId);

        FinishSim2DBallPath();
    }

    void BeginSim2DBallPath()
    {
        _sim2DBallPath.Clear();
        _sim2DBallFollowsHandler = false;
        _sim2DBallPath.Add(new Sim2DBallWaypoint
        {
            Position = new Vector2(_sim2DBallX, _sim2DBallY),
            Time = 0f
        });
    }

    void AddSim2DBallWaypoint(Vector2 point, float time, bool isShot = false)
    {
        if (_sim2DBallPath.Count == 0) BeginSim2DBallPath();
        float previousTime = _sim2DBallPath[_sim2DBallPath.Count - 1].Time;
        _sim2DBallPath.Add(new Sim2DBallWaypoint
        {
            Position = new Vector2(Mathf.Clamp(point.x, 0.015f, 0.985f),
                Mathf.Clamp(point.y, 0.02f, 0.98f)),
            Time = Mathf.Clamp(Mathf.Max(time, previousTime + 0.001f), 0f, 1f),
            IsShot = isShot
        });
    }

    void FinishSim2DBallPath()
    {
        if (_sim2DBallPath.Count == 0) return;
        var last = _sim2DBallPath[_sim2DBallPath.Count - 1].Position;
        _sim2DBallTargetX = last.x;
        _sim2DBallTargetY = last.y;
    }

    Vector2 GetSim2DShotPosition(bool threePointer, bool attackRight, int eventIndex, string position)
    {
        float side = (eventIndex % 2 == 0) ? 1f : -1f;
        Vector2 point;
        if (threePointer)
            point = new Vector2(0.75f, side > 0 ? 0.20f : 0.80f);
        else if (position is "C" or "PF")
            point = new Vector2(0.86f, side * 0.05f + 0.5f);
        else
            point = new Vector2(0.82f, side > 0 ? 0.40f : 0.60f);

        if (!attackRight) point.x = 1f - point.x;
        return point;
    }

    Vector2 GetSim2DReboundPosition(Sim2DPlayerState player, bool attackRight)
    {
        float hoopX = attackRight ? SIM2D_RIGHT_HOOP_X : SIM2D_LEFT_HOOP_X;
        float towardCenter = attackRight ? -1f : 1f;
        string position = player?.Player.position;
        float y = position switch
        {
            "PF" => 0.42f,
            "C" => 0.58f,
            _ => 0.50f
        };
        return new Vector2(hoopX + towardCenter * 0.06f, y);
    }

    void SetSim2DTarget(Sim2DPlayerState player, Vector2 target, float pathTime = 0.86f)
    {
        if (player == null) return;
        player.TargetX = Mathf.Clamp(target.x, 0.025f, 0.975f);
        player.TargetY = Mathf.Clamp(target.y, 0.04f, 0.96f);
        if (_sim2DPlayerPaths.ContainsKey(player.Player.id))
            AddSim2DPlayerWaypoint(player.Player.id,
                new Vector2(player.TargetX, player.TargetY), pathTime);
    }

    void SetSim2DFormationTarget(Sim2DPlayerState player, Vector2 target, float pathTime)
    {
        if (player == null) return;
        SetSim2DTarget(player, target, pathTime);
        if (!_sim2DPlayerPaths.ContainsKey(player.Player.id))
            AddSim2DPlayerWaypoint(player.Player.id,
                new Vector2(player.TargetX, player.TargetY), pathTime);
    }

    void AddSim2DPlayerWaypoint(int playerId, Vector2 point, float time)
    {
        if (!_sim2DPlayers.TryGetValue(playerId, out var player)) return;
        if (!_sim2DPlayerPaths.TryGetValue(playerId, out var path))
        {
            path = new List<Sim2DPlayerWaypoint>();
            path.Add(new Sim2DPlayerWaypoint
            {
                Position = new Vector2(player.X, player.Y),
                Time = 0f
            });
            _sim2DPlayerPaths[playerId] = path;
        }

        float previousTime = path[path.Count - 1].Time;
        path.Add(new Sim2DPlayerWaypoint
        {
            Position = new Vector2(Mathf.Clamp(point.x, -0.02f, 1.02f),
                Mathf.Clamp(point.y, 0.04f, 0.96f)),
            Time = Mathf.Clamp(Mathf.Max(time, previousTime + 0.001f), 0f, 1f)
        });
    }

    void SetupSim2DTipOff(int pointGuardId, bool attackRight)
    {
        int homeJumperId = FindSim2DJumpBallPlayer(_sim2DHomeOnCourt);
        int awayJumperId = FindSim2DJumpBallPlayer(_sim2DAwayOnCourt);
        var normalTargets = _sim2DPlayers.Values
            .Where(player => player.OnCourt)
            .ToDictionary(player => player.Player.id,
                player => new Vector2(player.TargetX, player.TargetY));

        SetupTipOffTeam(_sim2DHomeOnCourt, homeJumperId, pointGuardId,
            true, attackRight, normalTargets);
        SetupTipOffTeam(_sim2DAwayOnCourt, awayJumperId, 0,
            false, attackRight, normalTargets);

        if (!normalTargets.TryGetValue(pointGuardId, out var guardSet)) return;
        float receiveX = attackRight ? 0.42f : 0.58f;
        Vector2 receivePoint = new Vector2(receiveX, 0.5f);
        Vector2 jumpTipPoint = homeJumperId > 0
            ? new Vector2(0.49f, 0.42f)
            : new Vector2(0.5f, 0.5f);
        AddSim2DBallWaypoint(new Vector2(0.5f, 0.5f), 0.14f);
        AddSim2DBallWaypoint(jumpTipPoint, 0.22f);
        AddSim2DBallWaypoint(receivePoint, 0.36f);
        AddSim2DBallWaypoint(guardSet, 0.46f);
        _sim2DBallHandlerId = pointGuardId;
        _sim2DBallFollowsHandler = true;
    }

    void SetupTipOffTeam(List<int> courtIds, int jumperId, int pointGuardId,
                         bool isHome, bool attackRight, Dictionary<int, Vector2> normalTargets)
    {
        var ids = OrderSim2DOnCourt(courtIds).Where(normalTargets.ContainsKey).ToList();
        int nonJumperSlot = 0;
        foreach (int id in ids)
        {
            if (!_sim2DPlayers.TryGetValue(id, out var player)) continue;
            Vector2 startSpot;
            if (id == jumperId)
            {
                startSpot = new Vector2(isHome ? 0.49f : 0.51f,
                    isHome ? 0.42f : 0.58f);
            }
            else
            {
                int slot = nonJumperSlot++;
                float x = isHome
                    ? (slot % 2 == 0 ? 0.29f : 0.41f)
                    : (slot % 2 == 0 ? 0.71f : 0.59f);
                float y = slot switch
                {
                    0 => 0.20f,
                    1 => 0.80f,
                    2 => 0.36f,
                    _ => 0.64f
                };
                startSpot = new Vector2(x, y);
            }

            var regularSpot = normalTargets[id];
            player.TargetX = startSpot.x;
            player.TargetY = startSpot.y;
            AddSim2DPlayerWaypoint(id, startSpot, 0.14f);
            if (id == pointGuardId && isHome)
            {
                float receiveX = attackRight ? 0.42f : 0.58f;
                AddSim2DPlayerWaypoint(id, new Vector2(receiveX, 0.5f), 0.32f);
                AddSim2DPlayerWaypoint(id, regularSpot, 0.46f);
            }
            else
                AddSim2DPlayerWaypoint(id, regularSpot, 0.74f);

            player.TargetX = regularSpot.x;
            player.TargetY = regularSpot.y;
        }
    }

    int FindSim2DJumpBallPlayer(List<int> courtIds)
    {
        return OrderSim2DOnCourt(courtIds)
            .OrderBy(id => _sim2DPlayers[id].Player.position == "C" ? 0
                : _sim2DPlayers[id].Player.position == "PF" ? 1 : 2)
            .ThenByDescending(id => _sim2DPlayers[id].Player.rebounding)
            .FirstOrDefault();
    }

    void SetupSim2DInbound(List<int> offenseIds, bool attackRight)
    {
        int pointGuardId = FindSim2DPointGuardForCurrentOffense();
        if (pointGuardId <= 0) return;
        if (_sim2DInboundType == Sim2DInboundType.TipOff)
        {
            SetupSim2DTipOff(pointGuardId, attackRight);
            return;
        }
        if (_sim2DInboundType == Sim2DInboundType.Outlet)
        {
            int rebounderId = _sim2DBallHandlerId;
            if (!_sim2DPlayers.TryGetValue(rebounderId, out var rebounder)
                || !_sim2DPlayers.TryGetValue(pointGuardId, out var outletGuard)) return;

            Vector2 guardSet = GetSim2DPosition("PG", true, attackRight);
            SetSim2DTarget(outletGuard, guardSet);
            if (rebounderId == pointGuardId)
            {
                AddSim2DPlayerWaypoint(pointGuardId, guardSet, 0.52f);
                AddSim2DBallWaypoint(guardSet, 0.52f);
                _sim2DBallHandlerId = pointGuardId;
                _sim2DBallFollowsHandler = true;
                return;
            }

            SetSim2DTarget(rebounder, new Vector2(rebounder.X, rebounder.Y));
            rebounder.Node?.EnableInClassList("sim2d-player--action", true);
            float outletBackcourtX = attackRight ? 0.18f : 0.82f;
            float outletMidcourtX = attackRight ? 0.42f : 0.58f;
            Vector2 outletReceivePoint = new Vector2(outletBackcourtX, 0.5f);
            Vector2 outletMidcourtPoint = new Vector2(outletMidcourtX, 0.5f);
            AddSim2DPlayerWaypoint(rebounderId,
                new Vector2(rebounder.TargetX, rebounder.TargetY), 0.12f);
            AddSim2DPlayerWaypoint(pointGuardId, outletReceivePoint, 0.42f);
            AddSim2DPlayerWaypoint(pointGuardId, outletMidcourtPoint, 0.62f);
            AddSim2DPlayerWaypoint(pointGuardId, guardSet, 0.80f);
            AddSim2DBallWaypoint(new Vector2(rebounder.TargetX, rebounder.TargetY), 0.12f);
            AddSim2DBallWaypoint(outletReceivePoint, 0.42f);
            _sim2DBallHandlerId = pointGuardId;
            _sim2DBallFollowsHandler = true;
            return;
        }

        int inbounderId = offenseIds
            .Where(id => id != pointGuardId)
            .OrderBy(id => _sim2DPlayers[id].Player.position == "C" ? 0
                : _sim2DPlayers[id].Player.position == "PF" ? 1
                : _sim2DPlayers[id].Player.position == "SF" ? 2
                : _sim2DPlayers[id].Player.position == "SG" ? 3 : 4)
            .ThenByDescending(id => _sim2DPlayers[id].Player.rebounding)
            .FirstOrDefault();
        // Plantillas cortas: si solo queda un jugador, que saque y conduzca él.
        if (inbounderId <= 0) inbounderId = pointGuardId;
        if (!_sim2DPlayers.TryGetValue(inbounderId, out var inbounder)
            || !_sim2DPlayers.TryGetValue(pointGuardId, out var pointGuard)) return;

        float baselineX = attackRight ? 0.03f : 0.97f;
        float inboundX = _sim2DInboundType == Sim2DInboundType.Baseline
            ? baselineX
            : Mathf.Clamp(_sim2DBallX, 0.08f, 0.92f);
        float inboundY = _sim2DInboundType == Sim2DInboundType.Baseline
            ? 0.50f
            : _sim2DInboundY;
        Vector2 inbounderOffenseSpot = new Vector2(inbounder.TargetX, inbounder.TargetY);
        SetSim2DTarget(inbounder, new Vector2(inboundX, inboundY));
        if (_sim2DInboundType == Sim2DInboundType.Baseline)
            inbounder.TargetX = baselineX;
        inbounder.Node?.AddToClassList("sim2d-player--inbound");
        if (_sim2DInboundType == Sim2DInboundType.Baseline)
            AddSim2DBallWaypoint(new Vector2(_sim2DBallX, _sim2DBallY), 0.16f);
        AddSim2DPlayerWaypoint(inbounderId,
            new Vector2(inbounder.TargetX, inbounder.TargetY), 0.25f);
        if (inbounderId != pointGuardId)
        {
            float direction = attackRight ? 1f : -1f;
            Vector2 stepInPoint = new Vector2(baselineX + direction * 0.13f, 0.5f);
            Vector2 trailPoint = new Vector2(attackRight ? 0.63f : 0.37f,
                inbounderOffenseSpot.y);
            AddSim2DPlayerWaypoint(inbounderId, stepInPoint, 0.40f);
            AddSim2DPlayerWaypoint(inbounderId, trailPoint, 0.67f);
            inbounder.TargetX = trailPoint.x;
            inbounder.TargetY = trailPoint.y;
        }
        else
        {
            inbounder.TargetX = inbounderOffenseSpot.x;
            inbounder.TargetY = inbounderOffenseSpot.y;
        }

        float backcourtX = attackRight ? 0.12f : 0.88f;
        float midcourtX = attackRight ? 0.42f : 0.58f;
        float receiveY = Mathf.Clamp(inboundY + (inboundY < 0.5f ? 0.12f : -0.12f), 0.18f, 0.82f);
        Vector2 receivePoint = new Vector2(backcourtX, receiveY);
        Vector2 midcourtPoint = new Vector2(midcourtX, 0.5f);
        Vector2 pointGuardSet = GetSim2DPosition("PG", true, attackRight);
        if (pointGuardId == inbounderId)
        {
            pointGuard.TargetX = pointGuardSet.x;
            pointGuard.TargetY = pointGuardSet.y;
        }
        else
            SetSim2DTarget(pointGuard, pointGuardSet);
        AddSim2DPlayerWaypoint(pointGuardId, receivePoint, 0.42f);
        AddSim2DPlayerWaypoint(pointGuardId, midcourtPoint, 0.62f);
        AddSim2DPlayerWaypoint(pointGuardId, pointGuardSet, 0.80f);

        // La pelota parte del jugador situado junto a la línea, viaja a un base
        // claramente separado y este avanza desde campo propio hacia la pista rival.
        AddSim2DBallWaypoint(new Vector2(inbounder.TargetX, inbounder.TargetY), 0.28f);
        AddSim2DBallWaypoint(receivePoint, 0.42f);
        _sim2DBallHandlerId = pointGuardId;
        _sim2DBallFollowsHandler = true;
    }

    void LayoutSim2DFreeThrow(int shooterId, List<int> offenseIds, List<int> defenseIds,
                              bool attackRight)
    {
        if (!_sim2DPlayers.TryGetValue(shooterId, out var shooter)) return;
        float hoopX = attackRight ? SIM2D_RIGHT_HOOP_X : SIM2D_LEFT_HOOP_X;
        float ftX = attackRight ? SIM2D_RIGHT_FT_X : SIM2D_LEFT_FT_X;
        float towardHoop = attackRight ? 1f : -1f;
        SetSim2DFormationTarget(shooter, new Vector2(ftX, 0.5f), 0.48f);
        shooter.Node?.EnableInClassList("sim2d-player--action", true);

        var offenseRebounders = OrderSim2DRebounders(offenseIds.Where(id => id != shooterId).ToList());
        var defenseRebounders = OrderSim2DRebounders(defenseIds);
        for (int side = 0; side < 2; side++)
        {
            float y = side == 0 ? 0.39f : 0.61f;
            if (side < offenseRebounders.Count
                && _sim2DPlayers.TryGetValue(offenseRebounders[side], out var offensive))
                SetSim2DFormationTarget(offensive, new Vector2(hoopX - towardHoop * 0.10f, y), 0.42f);
            if (side < defenseRebounders.Count
                && _sim2DPlayers.TryGetValue(defenseRebounders[side], out var defensive))
                SetSim2DFormationTarget(defensive, new Vector2(hoopX - towardHoop * 0.045f, y), 0.42f);
        }

        int outsideOffenseIndex = 0;
        foreach (int id in offenseIds.Where(id => id != shooterId && !offenseRebounders.Take(2).Contains(id)))
        {
            if (!_sim2DPlayers.TryGetValue(id, out var player)) continue;
            float y = outsideOffenseIndex++ % 2 == 0 ? 0.25f : 0.75f;
            SetSim2DFormationTarget(player, new Vector2(ftX - towardHoop * 0.09f, y), 0.42f);
        }
        int outsideDefenseIndex = 0;
        foreach (int id in defenseIds.Where(id => !defenseRebounders.Take(2).Contains(id)))
        {
            if (!_sim2DPlayers.TryGetValue(id, out var player)) continue;
            int slot = outsideDefenseIndex++;
            float y = slot switch
            {
                0 => 0.25f,
                1 => 0.75f,
                _ => 0.50f
            };
            SetSim2DFormationTarget(player, new Vector2(ftX - towardHoop * 0.16f, y), 0.42f);
        }
    }

    List<int> OrderSim2DRebounders(List<int> ids)
    {
        return OrderSim2DOnCourt(ids)
            .OrderByDescending(id => _sim2DPlayers[id].Player.position is "C" or "PF" ? 1 : 0)
            .ThenByDescending(id => _sim2DPlayers[id].Player.rebounding)
            .ToList();
    }

    int FindNearestSim2DDefender(int attackerId)
    {
        if (!_sim2DPlayers.TryGetValue(attackerId, out var attacker)) return 0;
        var defenders = _sim2DPlayers.Values
            .Where(p => p.OnCourt && p.TeamId != attacker.TeamId)
            .OrderBy(p => Vector2.Distance(
                new Vector2(p.TargetX, p.TargetY),
                new Vector2(attacker.TargetX, attacker.TargetY)))
            .FirstOrDefault();
        return defenders?.Player.id ?? 0;
    }

    void ApplySim2DDefensivePressure(int eventIndex, int ballHandlerId)
    {
        if (ballHandlerId <= 0 || !_sim2DPlayers.TryGetValue(ballHandlerId, out var handler)) return;
        int defenderId = FindNearestSim2DDefender(ballHandlerId);
        if (defenderId <= 0 || !_sim2DPlayers.TryGetValue(defenderId, out var defender)) return;

        float pressure = 0.34f + 0.12f * (0.5f + 0.5f * Mathf.Sin(eventIndex * 0.8f));
        defender.TargetX = Mathf.Lerp(defender.TargetX, handler.TargetX, pressure);
        defender.TargetY = Mathf.Lerp(defender.TargetY, handler.TargetY, pressure);
        defender.Node?.AddToClassList("sim2d-player--pressure");
    }

    static int FindSim2DPlayerForStat(GameSimulator.PlayByPlayEvent ev, params string[] stats)
    {
        if (ev?.deltas == null || ev.deltas.Count == 0 || stats == null) return 0;
        foreach (string stat in stats)
        {
            var delta = ev.deltas.FirstOrDefault(d => d.stat == stat && d.amount > 0);
            if (delta != null) return delta.player_id;
        }
        return 0;
    }

    static float GetSim2DClockTarget(GameSimulator.PlayByPlayEvent current,
                                     GameSimulator.PlayByPlayEvent next)
    {
        if (current == null || current.text == "¡Fin del partido!" || next == null)
            return current?.timeElapsed ?? 0f;

        if (next.quarter == current.quarter)
            return Mathf.Max(current.timeElapsed, next.timeElapsed);

        return current.quarter <= 4 ? 12f : 5f;
    }

    IEnumerator AnimateSim2DPositions(int quarter, float clockStart, float clockTarget,
                                      float duration, float shotClockStart, float shotClockTarget)
    {
        duration = Mathf.Max(0.05f, duration);
        float clockDuration = duration;

        var start = new Dictionary<int, Vector2>();
        foreach (var state in _sim2DPlayers.Values)
            start[state.Player.id] = new Vector2(state.X, state.Y);
        float startBallX = _sim2DBallX, startBallY = _sim2DBallY;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            while (_sim2DPaused && !_sim2DFinished)
                yield return null;

            elapsed += Time.unscaledDeltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);
            float t = Mathf.SmoothStep(0f, 1f, rawT);
            UpdateSim2DClock(quarter, Mathf.Lerp(clockStart, clockTarget,
                Mathf.Clamp01(elapsed / clockDuration)));
            UpdateSim2DShotClock(Mathf.Lerp(shotClockStart, shotClockTarget, rawT));
            foreach (var state in _sim2DPlayers.Values)
            {
                if (!state.OnCourt) continue;
                Vector2 from = start[state.Player.id];
                Vector2 playerPosition = GetSim2DPlayerPositionAt(state, from, rawT, t);
                state.X = playerPosition.x;
                state.Y = playerPosition.y;
                ApplySim2DNodePosition(state);
            }

            UpdateSim2DBallAlongPath(rawT, elapsed, startBallX, startBallY);
            ApplySim2DBallPosition();
            yield return null;
        }

        foreach (var state in _sim2DPlayers.Values)
        {
            if (!state.OnCourt) continue;
            state.X = state.TargetX;
            state.Y = state.TargetY;
            ApplySim2DNodePosition(state);
        }
        if (_sim2DBallFollowsHandler && _sim2DPlayers.TryGetValue(_sim2DBallHandlerId, out var finalHandler))
        {
            Vector2 follow = GetSim2DBallFollowPosition(finalHandler, duration);
            _sim2DBallX = follow.x;
            _sim2DBallY = follow.y;
        }
        else
        {
            _sim2DBallX = _sim2DBallTargetX;
            _sim2DBallY = _sim2DBallTargetY;
        }
        ApplySim2DBallPosition();
    }

    Vector2 GetSim2DPlayerPositionAt(Sim2DPlayerState state, Vector2 start,
                                    float rawT, float easedT)
    {
        if (state == null || !_sim2DPlayerPaths.TryGetValue(state.Player.id, out var path)
            || path.Count == 0)
            return new Vector2(Mathf.Lerp(start.x, state.TargetX, easedT),
                Mathf.Lerp(start.y, state.TargetY, easedT));

        Vector2 from = start;
        float fromTime = 0f;
        foreach (var waypoint in path)
        {
            if (rawT <= waypoint.Time)
            {
                float segment = Mathf.Max(0.001f, waypoint.Time - fromTime);
                float t = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((rawT - fromTime) / segment));
                return Vector2.Lerp(from, waypoint.Position, t);
            }
            from = waypoint.Position;
            fromTime = waypoint.Time;
        }
        return path[path.Count - 1].Position;
    }

    void UpdateSim2DBallAlongPath(float t, float elapsed, float startX, float startY)
    {
        if (_sim2DBallFollowsHandler && _sim2DBallPath.Count > 0
            && t >= _sim2DBallPath[_sim2DBallPath.Count - 1].Time
            && _sim2DPlayers.TryGetValue(_sim2DBallHandlerId, out var handler))
        {
            Vector2 follow = GetSim2DBallFollowPosition(handler, elapsed);
            _sim2DBallX = follow.x;
            _sim2DBallY = follow.y;
            return;
        }

        if (_sim2DBallPath.Count < 2)
        {
            _sim2DBallX = Mathf.Lerp(startX, _sim2DBallTargetX, t);
            _sim2DBallY = Mathf.Lerp(startY, _sim2DBallTargetY, t);
            return;
        }

        for (int i = 1; i < _sim2DBallPath.Count; i++)
        {
            var from = _sim2DBallPath[i - 1];
            var to = _sim2DBallPath[i];
            if (t > to.Time && i < _sim2DBallPath.Count - 1) continue;

            float segment = Mathf.Max(0.001f, to.Time - from.Time);
            float k = Mathf.Clamp01((t - from.Time) / segment);
            _sim2DBallX = Mathf.Lerp(from.Position.x, to.Position.x, k);
            _sim2DBallY = Mathf.Lerp(from.Position.y, to.Position.y, k);
            if (to.IsShot)
                _sim2DBallY -= Mathf.Sin(k * Mathf.PI) * 0.12f;
            return;
        }
    }

    Vector2 GetSim2DBallFollowPosition(Sim2DPlayerState handler, float elapsed)
    {
        float direction = _sim2DHomeOnOffense ? 1f : -1f;
        float bounce = Mathf.Abs(Mathf.Sin(elapsed * 8.5f)) * 0.012f;
        return new Vector2(Mathf.Clamp(handler.X + direction * 0.028f, 0.02f, 0.98f),
            Mathf.Clamp(handler.Y + bounce, 0.02f, 0.98f));
    }

    void UpdateSim2DClock(int quarter, float elapsed)
    {
        float length = quarter <= 4 ? 12f : 5f;
        float remaining = Mathf.Max(0f, length - elapsed);
        int mm = Mathf.FloorToInt(remaining);
        int ss = Mathf.RoundToInt((remaining - mm) * 60f);
        if (ss >= 60) { mm++; ss = 0; }
        if (_sim2DClock != null) _sim2DClock.text = $"{mm:D2}:{ss:D2}";
    }

    void UpdateSim2DShotClock(float seconds)
    {
        _sim2DShotClock = Mathf.Clamp(seconds, 0f, 24f);
        if (_sim2DShotClockLabel != null)
            _sim2DShotClockLabel.text = Mathf.CeilToInt(_sim2DShotClock).ToString("D2");
    }

    static void ApplySim2DNodePosition(Sim2DPlayerState state)
    {
        if (state?.Node == null) return;
        state.Node.style.left = new Length(state.X * 100f, LengthUnit.Percent);
        state.Node.style.top = new Length(state.Y * 100f, LengthUnit.Percent);
    }

    void ApplySim2DBallPosition()
    {
        if (_sim2DBall == null) return;
        _sim2DBall.style.left = new Length(_sim2DBallX * 100f, LengthUnit.Percent);
        _sim2DBall.style.top = new Length(_sim2DBallY * 100f, LengthUnit.Percent);
    }

    static bool Sim2DIsFieldGoalMade(GameSimulator.PlayByPlayEvent ev)
    {
        return ev?.deltas != null && ev.deltas.Any(d => d.stat == "fgm" && d.amount > 0);
    }

    static bool Sim2DIsThreePointer(GameSimulator.PlayByPlayEvent ev)
    {
        return ev?.deltas != null && ev.deltas.Any(d =>
            (d.stat == "fg3m" || d.stat == "fg3a") && d.amount > 0);
    }

    void PlaySim2DScoreEffect(bool attackRight, bool threePointer, bool freeThrow = false)
    {
        if (!_sim2DActive || _sim2DEffectsLayer == null) return;

        float hoopX = attackRight ? SIM2D_RIGHT_HOOP_X : SIM2D_LEFT_HOOP_X;
        float hoopY = SIM2D_HOOP_Y;
        float duration = freeThrow ? 0.38f : threePointer ? 0.62f : 0.52f;

        var ring = new VisualElement { pickingMode = PickingMode.Ignore };
        ring.AddToClassList("sim2d-score-ring");
        ring.EnableInClassList("sim2d-score-ring--three", threePointer);
        SetSim2DScorePosition(ring, hoopX, hoopY);
        ring.style.width = 28f;
        ring.style.height = 28f;
        ring.style.marginLeft = -14f;
        ring.style.marginTop = -14f;
        ring.style.opacity = 1f;
        _sim2DEffectsLayer.Add(ring);

        int count = freeThrow ? 6 : threePointer ? 16 : 11;
        var particles = new List<Sim2DScoreParticle>(count);
        for (int i = 0; i < count; i++)
        {
            float size = UnityEngine.Random.Range(4.5f, 8.5f);
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList("sim2d-score-particle");
            element.style.width = size;
            element.style.height = size;
            element.style.marginLeft = -size * 0.5f;
            element.style.marginTop = -size * 0.5f;
            element.style.backgroundColor = new StyleColor(
                Sim2DScoreParticleColors[UnityEngine.Random.Range(0, Sim2DScoreParticleColors.Length)]);
            SetSim2DScorePosition(element, hoopX, hoopY);
            element.style.opacity = 1f;
            _sim2DEffectsLayer.Add(element);

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float speed = UnityEngine.Random.Range(0.055f, 0.14f);
            particles.Add(new Sim2DScoreParticle
            {
                Element = element,
                X = hoopX,
                Y = hoopY,
                Vx = Mathf.Cos(angle) * speed + (attackRight ? -0.035f : 0.035f),
                Vy = Mathf.Sin(angle) * speed - 0.045f
            });
        }

        _sim2DScoreEffectRoutines.Add(StartCoroutine(
            AnimateSim2DScoreEffect(ring, particles, duration)));
    }

    IEnumerator AnimateSim2DScoreEffect(VisualElement ring, List<Sim2DScoreParticle> particles,
                                       float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            while (_sim2DPaused && !_sim2DFinished)
                yield return null;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float ringSize = Mathf.Lerp(26f, 82f, t);
            ring.style.width = ringSize;
            ring.style.height = ringSize;
            ring.style.marginLeft = -ringSize * 0.5f;
            ring.style.marginTop = -ringSize * 0.5f;
            ring.style.opacity = 1f - t;

            float fade = Mathf.Clamp01(1f - Mathf.InverseLerp(0.35f, 1f, t));
            foreach (var particle in particles)
            {
                float x = particle.X + particle.Vx * elapsed;
                float y = particle.Y + particle.Vy * elapsed + 0.20f * elapsed * elapsed;
                SetSim2DScorePosition(particle.Element, x, y);
                particle.Element.style.opacity = fade;
            }
            yield return null;
        }

        ring.RemoveFromHierarchy();
        foreach (var particle in particles)
            particle.Element.RemoveFromHierarchy();
    }

    static void SetSim2DScorePosition(VisualElement element, float x, float y)
    {
        if (element == null) return;
        element.style.left = new Length(x * 100f, LengthUnit.Percent);
        element.style.top = new Length(y * 100f, LengthUnit.Percent);
    }

    void ClearSim2DScoreEffects()
    {
        foreach (var routine in _sim2DScoreEffectRoutines)
        {
            if (routine != null) StopCoroutine(routine);
        }
        _sim2DScoreEffectRoutines.Clear();
        _sim2DEffectsLayer?.Clear();
    }

    void OnSim2DComplete()
    {
        _sim2DFinished = true;
        _sim2DPaused = false;
        if (_sim2DPause != null)
        {
            _sim2DPause.text = LocalizationService.T("ui.code.match_day_controller.pausa");
            _sim2DPause.SetEnabled(false);
        }
        if (_sim2DSkip != null)
            _sim2DSkip.text = LocalizationService.T("ui.code.match_day_controller.ir_al_resumen");
        if (_sim2DTickerText != null)
            _sim2DTickerText.text = LocalizationService.T("ui.code.match_day_controller.fin_del_partido");
        _sim2DPossession.text = "";
    }

    void JumpToSimVisualEnd()
    {
        if (_sim2DRoutine != null) { StopCoroutine(_sim2DRoutine); _sim2DRoutine = null; }
        ClearSim2DScoreEffects();
        if (_sim2DLog == null || _sim2DLog.Count == 0) { OnSim2DComplete(); return; }

        foreach (var state in _sim2DPlayers.Values)
        {
            state.Points = 0;
            state.Rebounds = 0;
            state.Assists = 0;
        }
        List<int> lastHomeCourt = null;
        List<int> lastAwayCourt = null;
        bool lastHomeOffense = _sim2DHomeOnOffense;
        foreach (var ev in _sim2DLog)
        {
            ApplySim2DDeltas(ev.deltas);
            if (ev.homeOnCourt != null && ev.homeOnCourt.Count > 0)
            {
                lastHomeCourt = new List<int>(ev.homeOnCourt);
                lastAwayCourt = new List<int>(ev.awayOnCourt ?? new List<int>());
                lastHomeOffense = ev.homeOnOffense;
            }
        }
        if (lastHomeCourt != null)
        {
            _sim2DHomeOnCourt = lastHomeCourt;
            _sim2DAwayOnCourt = lastAwayCourt ?? new List<int>();
            _sim2DHomeOnOffense = lastHomeOffense;
        }

        var final = _sim2DLog[_sim2DLog.Count - 1];
        UpdateSim2DScoreboard(final.homeScore, final.awayScore, final.quarter, final.timeElapsed);
        _sim2DTickerText.text = LocalizationService.T("ui.code.match_day_controller.fin_del_partido");
        UpdateSim2DRoster();
        LayoutSim2DTargets(final, _sim2DLog.Count - 1, animate: false);
        OnSim2DComplete();
    }

    void OnSim2DSkip()
    {
        if (_sim2DFinished) { StopSimVisual(); return; }
        JumpToSimVisualEnd();
    }

    void ToggleSim2DPause()
    {
        if (_sim2DFinished) return;
        _sim2DPaused = !_sim2DPaused;
        _sim2DPause.text = LocalizationService.T(_sim2DPaused
            ? "ui.code.match_day_controller.reanudar"
            : "ui.code.match_day_controller.pausa");
    }

    void StopSimVisual()
    {
        _sim2DActive = false;
        if (_sim2DRoutine != null) { StopCoroutine(_sim2DRoutine); _sim2DRoutine = null; }
        ClearSim2DScoreEffects();
        if (_sim2DOverlay != null) _sim2DOverlay.style.display = DisplayStyle.None;
    }

    void OnDisable()
    {
        StopSimVisual();
    }

    static int NormalizeSim2DSpeed(int speed) => speed switch
    {
        5 => 5,
        10 => 10,
        20 => 20,
        _ => 1
    };

    void SelectSim2DSpeed(int speed)
    {
        _sim2DSpeed = NormalizeSim2DSpeed(speed);
        PlayerPrefs.SetInt(SIM2D_SPEED_PREF, _sim2DSpeed);
        PlayerPrefs.Save();
        UpdateSim2DSpeedButtons();
    }

    void UpdateSim2DSpeedButtons()
    {
        _sim2DSpeed1?.EnableInClassList("sim2d-speed-btn--active", _sim2DSpeed == 1);
        _sim2DSpeed5?.EnableInClassList("sim2d-speed-btn--active", _sim2DSpeed == 5);
        _sim2DSpeed10?.EnableInClassList("sim2d-speed-btn--active", _sim2DSpeed == 10);
        _sim2DSpeed20?.EnableInClassList("sim2d-speed-btn--active", _sim2DSpeed == 20);
    }
}
