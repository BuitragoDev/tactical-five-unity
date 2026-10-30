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

    private VisualElement _sim2DOverlay, _sim2DHomeLayer, _sim2DAwayLayer, _sim2DBall;
    private VisualElement _sim2DHomeLogo, _sim2DAwayLogo, _sim2DTeamList;
    private Label _sim2DHomeAbbr, _sim2DAwayAbbr, _sim2DHomeName, _sim2DAwayName;
    private Label _sim2DScore, _sim2DQuarter, _sim2DClock, _sim2DTickerText;
    private Label _sim2DPossession, _sim2DVenue;
    private Button _sim2DHomeTab, _sim2DAwayTab, _sim2DPause, _sim2DSkip;
    private Button _sim2DSpeed1, _sim2DSpeed3, _sim2DSpeed5, _sim2DSpeed10;

    private Coroutine _sim2DRoutine;
    private List<GameSimulator.PlayByPlayEvent> _sim2DLog;
    private bool _sim2DActive, _sim2DFinished, _sim2DPaused;
    private int _sim2DGameId, _sim2DHomeTeamId, _sim2DAwayTeamId, _sim2DSelectedTeamId;
    private int _sim2DSpeed = 5;
    private bool _sim2DHomeOnOffense = true;
    private List<int> _sim2DHomeOnCourt = new();
    private List<int> _sim2DAwayOnCourt = new();
    private float _sim2DBallX = 0.5f, _sim2DBallY = 0.5f;
    private float _sim2DBallTargetX = 0.5f, _sim2DBallTargetY = 0.5f;
    private float _sim2DBallViaX, _sim2DBallViaY;
    private float _sim2DBallReceiverX, _sim2DBallReceiverY;
    private bool _sim2DBallHasVia, _sim2DBallHasReceiver;

    private readonly Dictionary<int, Sim2DPlayerState> _sim2DPlayers = new();

    // Formaciones normalizadas para un ataque hacia la derecha. Si el ataque va hacia
    // la izquierda se reflejan en X; el pequeño jitter de cada evento da sensación de
    // movimiento sin inventar una reconstrucción táctica exacta.
    private static readonly Vector2[] Sim2DOffenseSpots =
    {
        new Vector2(0.72f, 0.50f), new Vector2(0.84f, 0.24f), new Vector2(0.84f, 0.76f),
        new Vector2(0.63f, 0.33f), new Vector2(0.67f, 0.64f)
    };

    private static readonly Vector2[] Sim2DDefenseSpots =
    {
        new Vector2(0.60f, 0.50f), new Vector2(0.53f, 0.24f), new Vector2(0.53f, 0.76f),
        new Vector2(0.50f, 0.33f), new Vector2(0.47f, 0.64f)
    };

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

        _sim2DHomeLayer = _root.Q<VisualElement>("Sim2DHomeLayer");
        _sim2DAwayLayer = _root.Q<VisualElement>("Sim2DAwayLayer");
        _sim2DBall = _root.Q<VisualElement>("Sim2DBall");
        _sim2DHomeLogo = _root.Q<VisualElement>("Sim2DHomeLogo");
        _sim2DAwayLogo = _root.Q<VisualElement>("Sim2DAwayLogo");
        _sim2DTeamList = _root.Q<VisualElement>("Sim2DTeamList");

        _sim2DHomeAbbr = _root.Q<Label>("Sim2DHomeAbbr");
        _sim2DAwayAbbr = _root.Q<Label>("Sim2DAwayAbbr");
        _sim2DHomeName = _root.Q<Label>("Sim2DHomeName");
        _sim2DAwayName = _root.Q<Label>("Sim2DAwayName");
        _sim2DScore = _root.Q<Label>("Sim2DScore");
        _sim2DQuarter = _root.Q<Label>("Sim2DQuarter");
        _sim2DClock = _root.Q<Label>("Sim2DClock");
        _sim2DTickerText = _root.Q<Label>("Sim2DTickerText");
        _sim2DPossession = _root.Q<Label>("Sim2DPossession");
        _sim2DVenue = _root.Q<Label>("Sim2DVenue");

        _sim2DHomeTab = _root.Q<Button>("Sim2DHomeTab");
        _sim2DAwayTab = _root.Q<Button>("Sim2DAwayTab");
        _sim2DPause = _root.Q<Button>("Sim2DPause");
        _sim2DSkip = _root.Q<Button>("Sim2DSkip");
        _sim2DSpeed1 = _root.Q<Button>("Sim2DSpeed1");
        _sim2DSpeed3 = _root.Q<Button>("Sim2DSpeed3");
        _sim2DSpeed5 = _root.Q<Button>("Sim2DSpeed5");
        _sim2DSpeed10 = _root.Q<Button>("Sim2DSpeed10");

        _sim2DSpeed = PlayerPrefs.GetInt(SIM2D_SPEED_PREF, 5);
        UpdateSim2DSpeedButtons();
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
        _sim2DSpeed3?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectSim2DSpeed(3); });
        _sim2DSpeed5?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectSim2DSpeed(5); });
        _sim2DSpeed10?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectSim2DSpeed(10); });

        if (CursorManager.Instance == null) return;
        foreach (var name in new[]
        {
            "Sim2DHomeTab", "Sim2DAwayTab", "Sim2DPause", "Sim2DSkip",
            "Sim2DSpeed1", "Sim2DSpeed3", "Sim2DSpeed5", "Sim2DSpeed10"
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

        var home = _allTeams?.Find(t => t.id == _sim2DHomeTeamId);
        var away = _allTeams?.Find(t => t.id == _sim2DAwayTeamId);
        _sim2DHomeAbbr.text = home?.abbreviation ?? "--";
        _sim2DAwayAbbr.text = away?.abbreviation ?? "--";
        _sim2DHomeName.text = home?.name?.ToUpperInvariant() ?? "";
        _sim2DAwayName.text = away?.name?.ToUpperInvariant() ?? "";
        SetLogo(_sim2DHomeLogo, home?.logo, "80x80");
        SetLogo(_sim2DAwayLogo, away?.logo, "80x80");
        _sim2DVenue.text = home?.arena ?? "";

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
        _sim2DTickerText.text = log.Count > 0 ? log[0].text : "";
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
        node.Add(photo);
        node.Add(plate);

        var layer = state.TeamId == _sim2DHomeTeamId ? _sim2DHomeLayer : _sim2DAwayLayer;
        layer?.Add(node);
        node.style.display = DisplayStyle.None;
        return node;
    }

    static string ShortName(PlayerData p)
    {
        if (p == null) return "";
        string last = string.IsNullOrWhiteSpace(p.last_name) ? p.first_name : p.last_name;
        return (last ?? "").ToUpperInvariant();
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
            row.Add(pos);

            var name = new Label { text = ShortName(state.Player) };
            name.AddToClassList("sim2d-roster-name");
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
        var team = _allTeams?.Find(t => t.id == teamId);
        _sim2DPossession.text = LocalizationService.T(
            "ui.code.match_day_controller.sim_ataque", team?.abbreviation ?? "");
    }

    IEnumerator Sim2DCoroutine(List<GameSimulator.PlayByPlayEvent> log)
    {
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
            _sim2DTickerText.text = ev.text;
            UpdateSim2DPossession();
            UpdateSim2DRoster();
            LayoutSim2DTargets(ev, i, animate: true);
            yield return AnimateSim2DPositions();

            float delay = Mathf.Max(0.04f, 0.55f / Mathf.Max(1, _sim2DSpeed));
            yield return new WaitForSecondsRealtime(delay);
        }
        OnSim2DComplete();
    }

    void LayoutSim2DTargets(GameSimulator.PlayByPlayEvent ev, int eventIndex, bool animate)
    {
        foreach (var state in _sim2DPlayers.Values)
        {
            state.OnCourt = false;
            state.TargetX = state.X;
            state.TargetY = state.Y;
        }

        LayoutSim2DTeam(_sim2DHomeOnCourt, true, eventIndex);
        LayoutSim2DTeam(_sim2DAwayOnCourt, false, eventIndex);

        foreach (var state in _sim2DPlayers.Values)
        {
            if (state.Node == null) continue;
            state.Node.style.display = state.OnCourt ? DisplayStyle.Flex : DisplayStyle.None;
        }

        int primaryId = FindSim2DPrimaryPlayer(ev);
        if (primaryId > 0 && _sim2DPlayers.TryGetValue(primaryId, out var primary) && primary.OnCourt)
        {
            _sim2DBallTargetX = primary.TargetX;
            _sim2DBallTargetY = primary.TargetY;
        }
        else
        {
            _sim2DBallTargetX = 0.5f;
            _sim2DBallTargetY = 0.5f;
        }

        int shooterId = FindSim2DPlayerForStat(ev, "pts", "fgm", "ftm", "fga", "fta");
        int assistId = FindSim2DPlayerForStat(ev, "ast");
        Sim2DPlayerState shooter = null;
        _sim2DBallHasReceiver = shooterId > 0
            && _sim2DPlayers.TryGetValue(shooterId, out shooter) && shooter.OnCourt;
        if (_sim2DBallHasReceiver)
        {
            _sim2DBallReceiverX = shooter.TargetX;
            _sim2DBallReceiverY = shooter.TargetY;
        }

        Sim2DPlayerState assister = null;
        _sim2DBallHasVia = _sim2DBallHasReceiver && assistId > 0 && assistId != shooterId
            && _sim2DPlayers.TryGetValue(assistId, out assister) && assister.OnCourt;
        if (_sim2DBallHasVia)
        {
            _sim2DBallViaX = assister.TargetX;
            _sim2DBallViaY = assister.TargetY;
        }

        bool scored = ev?.deltas != null && ev.deltas.Any(d => d.stat == "pts" && d.amount > 0);
        if (scored)
        {
            _sim2DBallTargetX = _sim2DHomeOnOffense ? 0.97f : 0.03f;
            _sim2DBallTargetY = 0.5f;
        }

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
        var spots = offense ? Sim2DOffenseSpots : Sim2DDefenseSpots;
        bool mirror = !_sim2DHomeOnOffense;

        for (int i = 0; i < ids.Count && i < spots.Length; i++)
        {
            if (!_sim2DPlayers.TryGetValue(ids[i], out var state)) continue;
            state.OnCourt = true;
            float x = spots[i].x;
            if (mirror) x = 1f - x;
            float jitterX = Mathf.Sin((eventIndex + 1) * 0.7f + i * 1.3f) * 0.012f;
            float jitterY = Mathf.Cos((eventIndex + 1) * 0.5f + i * 0.9f) * 0.012f;
            state.TargetX = Mathf.Clamp01(x + jitterX);
            state.TargetY = Mathf.Clamp01(spots[i].y + jitterY);
        }
    }

    static int FindSim2DPrimaryPlayer(GameSimulator.PlayByPlayEvent ev)
    {
        if (ev?.deltas == null || ev.deltas.Count == 0) return 0;
        string[] priority = { "pts", "fgm", "ftm", "oreb", "dreb", "ast", "stl", "blk", "to", "pf" };
        foreach (string stat in priority)
        {
            var delta = ev.deltas.FirstOrDefault(d => d.stat == stat && d.amount > 0);
            if (delta != null) return delta.player_id;
        }
        return 0;
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

    IEnumerator AnimateSim2DPositions()
    {
        float duration = Mathf.Max(0.04f, 0.18f / Mathf.Max(1, _sim2DSpeed));
        int steps = _sim2DBallHasVia ? 6 : 4;

        var start = new Dictionary<int, Vector2>();
        foreach (var state in _sim2DPlayers.Values)
            start[state.Player.id] = new Vector2(state.X, state.Y);
        float startBallX = _sim2DBallX, startBallY = _sim2DBallY;

        for (int step = 1; step <= steps; step++)
        {
            float t = (float)step / steps;
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
            yield return new WaitForSecondsRealtime(duration / steps);
        }
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

    void SelectSim2DSpeed(int speed)
    {
        _sim2DSpeed = speed;
        PlayerPrefs.SetInt(SIM2D_SPEED_PREF, speed);
        PlayerPrefs.Save();
        UpdateSim2DSpeedButtons();
    }

    void UpdateSim2DSpeedButtons()
    {
        _sim2DSpeed1?.EnableInClassList("sim2d-speed-btn--active", _sim2DSpeed == 1);
        _sim2DSpeed3?.EnableInClassList("sim2d-speed-btn--active", _sim2DSpeed == 3);
        _sim2DSpeed5?.EnableInClassList("sim2d-speed-btn--active", _sim2DSpeed == 5);
        _sim2DSpeed10?.EnableInClassList("sim2d-speed-btn--active", _sim2DSpeed == 10);
    }
}
