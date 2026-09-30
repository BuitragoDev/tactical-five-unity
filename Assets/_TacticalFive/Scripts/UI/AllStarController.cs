using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pantalla ALL-STAR WEEKEND (E5): convocatoria configurable (12 por conferencia, 5+7) y
/// resultados de los concursos de mates y triples. Los titulares se pueden alternar; el
/// resto lo compone la IA. La simulación de los concursos la hace `AllStarWeekendService`.
/// </summary>
public class AllStarController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.AllStar;

    private Button _tabConvocatoria, _tabMates, _tabTriples;
    private VisualElement _pageConvocatoria, _pageMates, _pageTriples;

    private VisualElement _eastHeader, _eastBody, _westHeader, _westBody;
    private VisualElement _dunkHeader, _dunkBody, _threeHeader, _threeBody;
    private Label _subtitle, _hint;
    private Button _btnRandomize;

    private Dictionary<string, Sprite> _logoSprites = new();
    private Texture2D _medalTex;
    private Texture2D _allStarIcon;

    // Convocatoria: candidatos por conferencia y qué jugadores son titulares (elegidos por el usuario)
    private List<AllStarCandidate> _eastField = new();
    private List<AllStarCandidate> _westField = new();
    private HashSet<int> _starterIds = new();

    protected override void CacheReferences()
    {
        _tabConvocatoria = _root.Q<Button>("TabConvocatoria");
        _tabMates = _root.Q<Button>("TabMates");
        _tabTriples = _root.Q<Button>("TabTriples");
        _pageConvocatoria = _root.Q<VisualElement>("PageConvocatoria");
        _pageMates = _root.Q<VisualElement>("PageMates");
        _pageTriples = _root.Q<VisualElement>("PageTriples");

        _eastHeader = _root.Q<VisualElement>("EastHeader");
        _eastBody = _root.Q<VisualElement>("EastBody");
        _westHeader = _root.Q<VisualElement>("WestHeader");
        _westBody = _root.Q<VisualElement>("WestBody");
        _dunkHeader = _root.Q<VisualElement>("DunkHeader");
        _dunkBody = _root.Q<VisualElement>("DunkBody");
        _threeHeader = _root.Q<VisualElement>("ThreeHeader");
        _threeBody = _root.Q<VisualElement>("ThreeBody");

        _subtitle = _root.Q<Label>("AllStarSubtitle");
        _hint = _root.Q<Label>("AllStarHint");
        _btnRandomize = _root.Q<Button>("BtnRandomizeStarters");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var screenIcon = _root.Q<VisualElement>("AllStarScreenIcon");
        _allStarIcon = Resources.Load<Texture2D>("Icons/estrella64px");
        if (_allStarIcon == null) _allStarIcon = Resources.Load<Texture2D>("Icons/record_icon");
        if (screenIcon != null && _allStarIcon != null)
            screenIcon.style.backgroundImage = new StyleBackground(_allStarIcon);

        foreach (var s in TeamImages.Logos(32))
            _logoSprites[s.name] = s;

        _medalTex = Resources.Load<Texture2D>("Icons/trofeo");

        BuildConvocatoria();
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();

        _tabConvocatoria?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("convocatoria"); });
        _tabMates?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("mates"); });
        _tabTriples?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("triples"); });
        _btnRandomize?.RegisterCallback<ClickEvent>(_ => { PlayClick(); AutoStarters(); BuildConvocatoria(); BuildContestPages(); });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_tabConvocatoria);
            CursorManager.Instance.RegisterHandCursor(_tabMates);
            CursorManager.Instance.RegisterHandCursor(_tabTriples);
            CursorManager.Instance.RegisterHandCursor(_btnRandomize);
        }
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[AllStar] RefreshHeader error: {ex.Message}"); }
        ShowTab("convocatoria");
    }

    void ShowTab(string tab)
    {
        SetActive(_tabConvocatoria, tab == "convocatoria");
        SetActive(_tabMates, tab == "mates");
        SetActive(_tabTriples, tab == "triples");

        SetVisible(_pageConvocatoria, tab == "convocatoria");
        SetVisible(_pageMates, tab == "mates");
        SetVisible(_pageTriples, tab == "triples");

        if (tab == "mates") BuildDunkPage();
        else if (tab == "triples") BuildThreePage();
    }

    static void SetActive(Button btn, bool active)
    {
        if (btn == null) return;
        if (active) btn.AddToClassList("standings-tab--active");
        else btn.RemoveFromClassList("standings-tab--active");
    }

    static void SetVisible(VisualElement elem, bool visible)
    {
        if (elem != null) elem.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ── CONVOCATORIA ─────────────────────────────────────

    void BuildConvocatoria()
    {
        if (_season == null || _manager == null) return;

        var allPlayers = DatabaseManager.Instance.GetAllPlayers()
            .Where(p => p != null && p.team_id > 0 && p.injury_days == 0 && p.g_league_assigned == 0)
            .ToList();
        var seasonStats = DatabaseManager.Instance.GetSeasonPlayerStatsAggregates(_manager.id, _season.id);

        _eastField = AllStarWeekendService.BuildConferenceRoster(_manager.id, _season.id, "East", allPlayers, seasonStats);
        _westField = AllStarWeekendService.BuildConferenceRoster(_manager.id, _season.id, "West", allPlayers, seasonStats);

        // Los titulares por defecto salen de la selección (5 mejores por puesto de cada conferencia).
        if (_starterIds.Count == 0)
            AutoStarters();

        if (_subtitle != null)
            _subtitle.text = $"Temporada {_season.year_start}-{_season.year_end} · 12 convocados por conferencia (5 titulares)";
        if (_hint != null)
            _hint.text = "Alterna TITULAR/SUPLENTE en cualquier jugador; la IA compone la conferencia contraria. Los concursos usan a los convocados por sus atributos (atletismo y tiro).";

        BuildConferenceRows(_eastHeader, _eastBody, _eastField);
        BuildConferenceRows(_westHeader, _westBody, _westField);
    }

    void AutoStarters()
    {
        _starterIds.Clear();
        foreach (var c in _eastField.Take(AllStarSelectionHelper.STARTERS)) _starterIds.Add(c.player_id);
        foreach (var c in _westField.Take(AllStarSelectionHelper.STARTERS)) _starterIds.Add(c.player_id);
    }

    void BuildConferenceRows(VisualElement header, VisualElement body, List<AllStarCandidate> field)
    {
        if (header == null || body == null) return;

        if (field == null || field.Count == 0)
        {
            header.Clear(); body.Clear();
            var empty = new Label("Sin convocados disponibles.");
            empty.AddToClassList("allstar-empty");
            body.Add(empty);
            return;
        }

        header.Clear();
        header.Add(HeaderLabel("POS", "allstar-col-pos"));
        header.Add(HeaderLabel("JUGADOR", "allstar-col-name"));
        header.Add(HeaderLabel("", "allstar-col-team"));
        header.Add(HeaderLabel("MED", "allstar-col-ovr"));
        header.Add(HeaderLabel("PUNTOS", "allstar-col-score"));
        header.Add(HeaderLabel("ROL", "allstar-col-role"));

        body.Clear();
        foreach (var c in field)
        {
            bool starter = _starterIds.Contains(c.player_id);
            bool mine = c.team_id == _myTeam.id;

            var row = new VisualElement();
            row.AddToClassList("allstar-row");
            if (starter) row.AddToClassList("allstar-row--starter");
            if (mine) row.AddToClassList("allstar-row--my-team");

            var pos = new Label(PositionCodes.GetShort(c.position));
            pos.AddToClassList("allstar-col-pos");
            row.Add(pos);

            var name = new Label(c.player_name + (mine ? "  ★" : ""));
            name.AddToClassList("allstar-col-name");
            row.Add(name);

            var logo = new VisualElement();
            logo.AddToClassList("allstar-col-team");
            // El logo del equipo se resuelve por el logo del TeamData.
            var team = DatabaseManager.Instance.GetTeamById(c.team_id);
            if (team != null && _logoSprites.TryGetValue(team.logo, out var sprite))
                logo.style.backgroundImage = new StyleBackground(sprite);
            row.Add(logo);

            var ovr = new Label(c.overall.ToString());
            ovr.AddToClassList("allstar-col-ovr");
            row.Add(ovr);

            var score = new Label($"{c.score:F1}");
            score.AddToClassList("allstar-col-score");
            row.Add(score);

            var role = new Label(starter ? "TITULAR" : "SUPLENTE");
            role.AddToClassList("allstar-col-role");
            role.AddToClassList(starter ? "allstar-col-role--starter" : "allstar-col-role--reserve");
            row.Add(role);

            int pid = c.player_id;
            row.RegisterCallback<ClickEvent>(_ => { PlayClick(); ToggleStarter(pid, field); });

            body.Add(row);
        }
    }

    void ToggleStarter(int playerId, List<AllStarCandidate> field)
    {
        // Una conferencia no puede pasar de 5 titulares: si ya hay 5, se quita el último.
        if (_starterIds.Contains(playerId))
        {
            _starterIds.Remove(playerId);
        }
        else
        {
            var confStarters = field.Where(c => _starterIds.Contains(c.player_id)).ToList();
            if (confStarters.Count >= AllStarSelectionHelper.STARTERS)
                _starterIds.Remove(confStarters.Last().player_id);
            _starterIds.Add(playerId);
        }

        BuildConferenceRows(_eastHeader, _eastBody, _eastField);
        BuildConferenceRows(_westHeader, _westBody, _westField);
    }

    static Label HeaderLabel(string text, string cls)
    {
        var lbl = new Label(text);
        lbl.AddToClassList(cls);
        return lbl;
    }

    // ── CONCURSOS ────────────────────────────────────────

    void BuildContestPages()
    {
        BuildDunkPage();
        BuildThreePage();
    }

    void BuildDunkPage()
    {
        var evt = LatestEvent(AllStarWeekendService.EVENT_SLAM_DUNK);
        BuildContestTable(_dunkHeader, _dunkBody, evt, "El concurso de mates se disputa la noche del All-Star. Aún no se ha celebrado.");
    }

    void BuildThreePage()
    {
        var evt = LatestEvent(AllStarWeekendService.EVENT_THREE_POINT);
        BuildContestTable(_threeHeader, _threeBody, evt, "El concurso de triples se disputa la noche del All-Star. Aún no se ha celebrado.");
    }

    AllStarEventData LatestEvent(string type)
    {
        if (_manager == null) return null;
        return DatabaseManager.Instance.GetAllStarEvents(_manager.id)
            .Where(e => e.event_type == type)
            .OrderByDescending(e => e.season_id)
            .FirstOrDefault();
    }

    void BuildContestTable(VisualElement header, VisualElement body, AllStarEventData evt, string emptyText)
    {
        if (header == null || body == null) return;

        if (evt == null || evt.season_id != (_season?.id ?? -1))
        {
            header.Clear(); body.Clear();
            var empty = new Label(emptyText);
            empty.AddToClassList("allstar-empty");
            body.Add(empty);
            return;
        }

        header.Clear();
        header.Add(HeaderLabel("#", "allstar-col-pos"));
        header.Add(HeaderLabel("JUGADOR", "allstar-col-name"));
        header.Add(HeaderLabel("", "allstar-col-team"));
        header.Add(HeaderLabel("PUNTOS", "allstar-col-score"));
        header.Add(HeaderLabel("", "allstar-col-score"));

        body.Clear();
        var participants = DatabaseManager.Instance.GetAllStarEventParticipants(evt.id);
        foreach (var p in participants)
        {
            var row = new VisualElement();
            row.AddToClassList("allstar-row");
            if (p.is_winner == 1) row.AddToClassList("allstar-row--starter");

            var rank = new Label(p.rank.ToString());
            rank.AddToClassList("allstar-col-pos");
            row.Add(rank);

            var name = new Label(p.player_name);
            name.AddToClassList("allstar-col-name");
            row.Add(name);

            var logo = new VisualElement();
            logo.AddToClassList("allstar-col-team");
            if (!string.IsNullOrEmpty(p.team_logo) && _logoSprites.TryGetValue(p.team_logo, out var sprite))
                logo.style.backgroundImage = new StyleBackground(sprite);
            row.Add(logo);

            var score = new Label(p.score.ToString());
            score.AddToClassList("allstar-col-score");
            row.Add(score);

            var badge = new VisualElement();
            badge.AddToClassList("allstar-medal");
            if (p.is_winner == 1 && _medalTex != null)
                badge.style.backgroundImage = new StyleBackground(_medalTex);
            row.Add(badge);

            body.Add(row);
        }
    }
}
