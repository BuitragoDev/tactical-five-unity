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

    private VisualElement _sim2DOverlay, _sim2DCourt, _sim2DHomeLayer, _sim2DAwayLayer, _sim2DBall;
    private VisualElement _sim2DHomeLogo, _sim2DAwayLogo, _sim2DTeamList;
    private VisualElement _sim2DLeftHoopLogo, _sim2DRightHoopLogo;
    private VisualElement _sim2DLeftAttackArrow, _sim2DRightAttackArrow;
    private Label _sim2DHomeAbbr, _sim2DAwayAbbr, _sim2DHomeName, _sim2DAwayName;
    private Label _sim2DScore, _sim2DQuarter, _sim2DClock, _sim2DTickerText;
    private Label _sim2DPossession, _sim2DVenue;
    private VisualElement _sim2DTickerIcon;
    private Label _sim2DHeaderPlayer, _sim2DHeaderPts, _sim2DHeaderReb, _sim2DHeaderAsi;
    private Button _sim2DHomeTab, _sim2DAwayTab, _sim2DPause, _sim2DSkip;
    private Button _sim2DSpeed1, _sim2DSpeed5, _sim2DSpeed10, _sim2DSpeed20;

    private Coroutine _sim2DRoutine;
    private List<GameSimulator.PlayByPlayEvent> _sim2DLog;
    private bool _sim2DActive, _sim2DFinished, _sim2DPaused;
    private int _sim2DGameId, _sim2DHomeTeamId, _sim2DAwayTeamId, _sim2DSelectedTeamId;
    private int _sim2DSpeed = 1;
    private bool _sim2DHomeOnOffense = true;
    private List<int> _sim2DHomeOnCourt = new();
    private List<int> _sim2DAwayOnCourt = new();
    private float _sim2DBallX = 0.5f, _sim2DBallY = 0.5f;
    private float _sim2DBallTargetX = 0.5f, _sim2DBallTargetY = 0.5f;
    private float _sim2DBallViaX, _sim2DBallViaY;
    private float _sim2DBallReceiverX, _sim2DBallReceiverY;
    private bool _sim2DBallHasVia, _sim2DBallHasReceiver;

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

    void CacheSimVisualReferences()
    {
        _sim2DOverlay = _root.Q<VisualElement>("SimVisualOverlay");
        if (_sim2DOverlay == null) return;

        _sim2DCourt = _root.Q<VisualElement>("Sim2DCourt");
        _sim2DHomeLayer = _root.Q<VisualElement>("Sim2DHomeLayer");
        _sim2DAwayLayer = _root.Q<VisualElement>("Sim2DAwayLayer");
        _sim2DBall = _root.Q<VisualElement>("Sim2DBall");
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

        _sim2DGameId = gameId;
        _sim2DLog = log;
        _sim2DActive = true;
        _sim2DFinished = false;
        _sim2DPaused = false;
        _sim2DHomeOnOffense = true;
        _sim2DHomeOnCourt = new List<int>();
        _sim2DAwayOnCourt = new List<int>();

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
        float previousAbsolute = 0f;
        for (int i = 0; i < log.Count; i++)
        {
            while (_sim2DPaused) yield return null;
            var ev = log[i];
            ApplySim2DDeltas(ev.deltas);
            if (ev.homeOnCourt != null && ev.homeOnCourt.Count > 0)
            {
                _sim2DHomeOnCourt = new List<int>(ev.homeOnCourt);
                _sim2DAwayOnCourt = new List<int>(ev.awayOnCourt ?? new List<int>());
                _sim2DHomeOnOffense = ev.homeOnOffense;
            }

            UpdateSim2DScoreboard(ev.homeScore, ev.awayScore, ev.quarter, ev.timeElapsed);
            _sim2DTickerText.text = Sim2DEventText(ev);
            UpdateSim2DPossession();
            UpdateSim2DRoster();
            LayoutSim2DTargets(ev, i, animate: true);

            // x1 = tiempo real: cada minuto de juego dura un minuto. Los eventos con
            // el mismo instante reciben un mínimo para que la acción sea visible.
            float currentAbsolute = GetSim2DAbsoluteMinutes(ev);
            float eventDuration = Mathf.Max(0.22f,
                (currentAbsolute - previousAbsolute) * 60f / Mathf.Max(1, _sim2DSpeed));
            previousAbsolute = currentAbsolute;

            var next = i + 1 < log.Count ? log[i + 1] : null;
            float clockTarget = GetSim2DClockTarget(ev, next);
            yield return AnimateSim2DPositions(ev.quarter, ev.timeElapsed, clockTarget,
                eventDuration);
        }
        OnSim2DComplete();
    }

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
        float width = evt.newRect.width;
        if (width < 10f) return;

        float targetHeight = width / SIM2D_COURT_ASPECT;
        if (Mathf.Abs(_sim2DCourt.resolvedStyle.height - targetHeight) < 0.5f) return;

        _sim2DCourt.style.flexGrow = 0;
        _sim2DCourt.style.flexShrink = 0;
        // El USS usa flex:1 (flex-basis:0). Si solo se cambia height, el layout
        // sigue midiendo por flex-basis y la cancha se queda a 0 px.
        _sim2DCourt.style.flexBasis = targetHeight;
        _sim2DCourt.style.height = targetHeight;
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
        }

        LayoutSim2DTeam(_sim2DHomeOnCourt, true, eventIndex);
        LayoutSim2DTeam(_sim2DAwayOnCourt, false, eventIndex);

        foreach (var state in _sim2DPlayers.Values)
        {
            if (state.Node == null) continue;
            state.Node.style.display = state.OnCourt ? DisplayStyle.Flex : DisplayStyle.None;
        }

        _sim2DBallTargetX = 0.5f;
        _sim2DBallTargetY = 0.5f;
        _sim2DBallHasReceiver = false;
        _sim2DBallHasVia = false;
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
            float phase = eventIndex * 0.72f + state.Player.id * 0.113f;
            float amplitude = state.Player.position switch
            {
                "PG" => 0.047f,
                "SG" or "SF" => 0.054f,
                "PF" => 0.037f,
                "C" => 0.026f,
                _ => 0.04f
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
            "PG" => new Vector2(offense ? 0.58f : 0.53f, 0.50f),
            "SG" => new Vector2(offense ? 0.73f : 0.66f, 0.18f),
            "SF" => new Vector2(offense ? 0.75f : 0.67f, 0.82f),
            "PF" => new Vector2(offense ? 0.88f : 0.79f, 0.32f),
            "C"  => new Vector2(offense ? 0.90f : 0.82f, 0.68f),
            _    => new Vector2(offense ? 0.70f : 0.64f, 0.5f)
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
        bool freeThrow = !shotAttempt && ev.deltas.Any(d => d.stat == "fta" && d.amount > 0);
        bool attackRight = _sim2DHomeOnOffense;

        // Cada atacante se recoloca desde su posición y función; el balón y los
        // defensores responden a los eventos reales del motor.
        if (shotAttempt && shooterId > 0 && _sim2DPlayers.TryGetValue(shooterId, out var shooter))
        {
            if (!threePointAttempt)
            {
                float hoopX = attackRight ? 0.91f : 0.09f;
                shooter.TargetX = Mathf.Lerp(shooter.TargetX, hoopX, 0.66f);
                shooter.TargetY = Mathf.Lerp(shooter.TargetY, 0.50f, 0.44f);
            }
            shooter.Node?.EnableInClassList("sim2d-player--action", true);

            int defenderId = blockId > 0 ? blockId : FindNearestSim2DDefender(shooterId);
            if (defenderId > 0 && _sim2DPlayers.TryGetValue(defenderId, out var defender))
            {
                defender.TargetX = Mathf.Lerp(defender.TargetX, shooter.TargetX, blockId > 0 ? 0.88f : 0.68f);
                defender.TargetY = Mathf.Lerp(defender.TargetY, shooter.TargetY, blockId > 0 ? 0.88f : 0.68f);
                // La defensa llega a puntear todos los tiros; solo el delta real blk
                // enciende el efecto de tapón y desvía la trayectoria del balón.
                defender.Node?.EnableInClassList("sim2d-player--action", blockId > 0);
            }
        }

        if (turnoverId > 0 && _sim2DPlayers.TryGetValue(turnoverId, out var handler))
        {
            float driveX = attackRight ? 0.09f : -0.09f;
            handler.TargetX = Mathf.Clamp(handler.TargetX + driveX, 0.04f, 0.96f);
            handler.TargetY = Mathf.Lerp(handler.TargetY, 0.50f, 0.25f);
        }
        else if (!shotAttempt && stealId == 0 && blockId == 0 && reboundId == 0)
        {
            int handlerId = FindSim2DPrimaryPlayerFromCurrentEvent();
            if (_sim2DPlayers.TryGetValue(handlerId, out var ballHandler))
            {
                float direction = attackRight ? 1f : -1f;
                ballHandler.TargetX = Mathf.Clamp(ballHandler.TargetX + direction * 0.045f, 0.04f, 0.96f);
                _sim2DBallTargetX = ballHandler.TargetX;
                _sim2DBallTargetY = ballHandler.TargetY;
            }
        }

        if (stealId > 0 && _sim2DPlayers.TryGetValue(stealId, out var thief))
        {
            if (turnoverId > 0 && _sim2DPlayers.TryGetValue(turnoverId, out var victim))
            {
                thief.TargetX = Mathf.Lerp(thief.TargetX, victim.TargetX, 0.76f);
                thief.TargetY = Mathf.Lerp(thief.TargetY, victim.TargetY, 0.76f);
                victim.Node?.EnableInClassList("sim2d-player--action", true);
            }
            _sim2DBallTargetX = thief.TargetX;
            _sim2DBallTargetY = thief.TargetY;
            thief.Node?.EnableInClassList("sim2d-player--action", true);
        }
        else if (blockId > 0 && _sim2DPlayers.TryGetValue(blockId, out var blocker))
        {
            _sim2DBallTargetX = Mathf.Clamp(blocker.TargetX + (attackRight ? 0.1f : -0.1f), 0.04f, 0.96f);
            _sim2DBallTargetY = Mathf.Clamp(blocker.TargetY + 0.12f, 0.08f, 0.92f);
            blocker.Node?.EnableInClassList("sim2d-player--action", true);
        }
        else if (reboundId > 0 && _sim2DPlayers.TryGetValue(reboundId, out var rebounder))
        {
            float hoopX = attackRight ? 0.91f : 0.09f;
            rebounder.TargetX = Mathf.Lerp(rebounder.TargetX, hoopX, 0.48f);
            rebounder.TargetY = Mathf.Lerp(rebounder.TargetY, 0.50f, 0.36f);
            _sim2DBallTargetX = rebounder.TargetX;
            _sim2DBallTargetY = rebounder.TargetY;
        }
        else if (shotAttempt && shooterId > 0 && _sim2DPlayers.TryGetValue(shooterId, out var shooterForBall))
        {
            _sim2DBallTargetX = madeShot ? (attackRight ? 0.97f : 0.03f) : shooterForBall.TargetX;
            _sim2DBallTargetY = 0.5f;
        }
        else if (turnoverId > 0 && _sim2DPlayers.TryGetValue(turnoverId, out var ballLost))
        {
            _sim2DBallTargetX = ballLost.TargetX;
            _sim2DBallTargetY = Mathf.Clamp(ballLost.TargetY + 0.16f, 0.08f, 0.92f);
        }
        else if (freeThrow)
        {
            int freeThrowShooterId = FindSim2DPlayerForStat(ev, "ftm", "fta", "pts");
            if (_sim2DPlayers.TryGetValue(freeThrowShooterId, out var freeThrowShooter))
            {
                float hoopX = attackRight ? 0.91f : 0.09f;
                freeThrowShooter.TargetX = Mathf.Lerp(freeThrowShooter.TargetX, hoopX, 0.32f);
                freeThrowShooter.TargetY = 0.50f;
                _sim2DBallTargetX = freeThrowShooter.TargetX;
                _sim2DBallTargetY = freeThrowShooter.TargetY;
            }
        }

        Sim2DPlayerState receiver = null;
        _sim2DBallHasReceiver = shotAttempt && shooterId > 0
            && _sim2DPlayers.TryGetValue(shooterId, out receiver) && receiver.OnCourt;
        if (_sim2DBallHasReceiver)
        {
            _sim2DBallReceiverX = receiver.TargetX;
            _sim2DBallReceiverY = receiver.TargetY;
        }

        int assistId = FindSim2DPlayerForStat(ev, "ast");
        Sim2DPlayerState passer = null;
        _sim2DBallHasVia = _sim2DBallHasReceiver && assistId > 0 && assistId != shooterId
            && _sim2DPlayers.TryGetValue(assistId, out passer) && passer.OnCourt;
        if (_sim2DBallHasVia)
        {
            _sim2DBallViaX = passer.TargetX;
            _sim2DBallViaY = passer.TargetY;
        }

        // La defensa presiona y cierra líneas de pase en cada posesión. Los robos y
        // tapones visibles como resultado solo aparecen cuando GameSimulator los anotó.
        if (stealId == 0 && blockId == 0)
        {
            int pressureTargetId = shooterId > 0 ? shooterId
                : turnoverId > 0 ? turnoverId
                : FindSim2DPrimaryPlayerFromCurrentEvent();
            ApplySim2DDefensivePressure(eventIndex, pressureTargetId);
        }
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

    int FindSim2DPrimaryPlayerFromCurrentEvent()
    {
        // El primer jugador ofensivo se usa solo para organizar la presión defensiva
        // cuando el evento no identifica un tirador, reboteador, robo o tapón.
        var ids = _sim2DHomeOnOffense ? _sim2DHomeOnCourt : _sim2DAwayOnCourt;
        return OrderSim2DOnCourt(ids)
            .OrderBy(id => Sim2DPositionOrder(_sim2DPlayers[id].Player.position))
            .ThenByDescending(id => _sim2DPlayers[id].Player.passing)
            .FirstOrDefault();
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
                                      float duration)
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
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            UpdateSim2DClock(quarter, Mathf.Lerp(clockStart, clockTarget,
                Mathf.Clamp01(elapsed / clockDuration)));
            foreach (var state in _sim2DPlayers.Values)
            {
                if (!state.OnCourt) continue;
                Vector2 from = start[state.Player.id];
                state.X = Mathf.Lerp(from.x, state.TargetX, t);
                state.Y = Mathf.Lerp(from.y, state.TargetY, t);
                ApplySim2DNodePosition(state);
            }

            // Con asistencia, el balón pasa por el asistente y luego por el tirador
            // antes de llegar al aro. Sin asistencia va directo al desenlace.
            if (_sim2DBallHasVia)
            {
                if (t <= 0.34f)
                {
                    float k = t / 0.34f;
                    _sim2DBallX = Mathf.Lerp(startBallX, _sim2DBallViaX, k);
                    _sim2DBallY = Mathf.Lerp(startBallY, _sim2DBallViaY, k);
                }
                else if (t <= 0.67f && _sim2DBallHasReceiver)
                {
                    float k = (t - 0.34f) / 0.33f;
                    _sim2DBallX = Mathf.Lerp(_sim2DBallViaX, _sim2DBallReceiverX, k);
                    _sim2DBallY = Mathf.Lerp(_sim2DBallViaY, _sim2DBallReceiverY, k);
                }
                else
                {
                    float k = (t - 0.67f) / 0.33f;
                    float fromX = _sim2DBallHasReceiver ? _sim2DBallReceiverX : _sim2DBallViaX;
                    float fromY = _sim2DBallHasReceiver ? _sim2DBallReceiverY : _sim2DBallViaY;
                    _sim2DBallX = Mathf.Lerp(fromX, _sim2DBallTargetX, k);
                    _sim2DBallY = Mathf.Lerp(fromY, _sim2DBallTargetY, k);
                }
            }
            else
            {
                _sim2DBallX = Mathf.Lerp(startBallX, _sim2DBallTargetX, t);
                _sim2DBallY = Mathf.Lerp(startBallY, _sim2DBallTargetY, t);
            }
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
        _sim2DBallX = _sim2DBallTargetX;
        _sim2DBallY = _sim2DBallTargetY;
        ApplySim2DBallPosition();
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
