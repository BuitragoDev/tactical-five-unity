using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class GLeagueController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.GLeague;

    // ── Pestañas ──
    private Button _tabPlantilla, _tabResultados, _tabClasificacion, _tabEstadisticas;
    private VisualElement _pagePlantilla, _pageResultados, _pageClasificacion, _pageEstadisticas;
    private string _currentTab = "plantilla";
    private string _standingsConf = "East";

    // ── Página Plantilla (existente) ──
    private Label _summaryAssigned;
    private Label _summaryTwoWay;
    private Label _assignedTitle;
    private VisualElement _assignedHeader;
    private VisualElement _assignedBody;
    private VisualElement _eligibleHeader;
    private VisualElement _eligibleBody;

    // ── Página Clasificación / Resultados / Líderes ──
    private Button _glTabEast, _glTabWest;
    private VisualElement _standingsBody;
    private VisualElement _resultsBody;
    private VisualElement _resultsHeader;
    private Label _resultsAffiliate;
    private VisualElement _leadersPts, _leadersReb, _leadersAst, _leadersStl, _leadersBlk, _leadersVal;

    // ── Datos ──
    private List<PlayerData> _players = new();
    private List<GLeagueTeamData> _glTeams = new();
    private List<GameData> _glGames = new();
    private List<GLeagueSeasonStat> _glStats = new();
    private GLeagueTeamData _myAffiliate;
    private Dictionary<string, Sprite> _glLogos = new();

    // Identidad de jugadores G-League (simId → nombre/filial)
    private Dictionary<int, string> _glNames = new();
    private Dictionary<int, int> _glTeamOfPlayer = new();
    private Dictionary<int, (string photo, bool isReal)> _glPhotos = new();

    protected override void CacheReferences()
    {
        // Pestañas principales
        _tabPlantilla = _root.Q<Button>("TabPlantilla");
        _tabResultados = _root.Q<Button>("TabResultados");
        _tabClasificacion = _root.Q<Button>("TabClasificacion");
        _tabEstadisticas = _root.Q<Button>("TabEstadisticas");
        _pagePlantilla = _root.Q<VisualElement>("PagePlantilla");
        _pageResultados = _root.Q<VisualElement>("PageResultados");
        _pageClasificacion = _root.Q<VisualElement>("PageClasificacion");
        _pageEstadisticas = _root.Q<VisualElement>("PageEstadisticas");

        // Plantilla
        _summaryAssigned = _root.Q<Label>("SummaryAssigned");
        _summaryTwoWay = _root.Q<Label>("SummaryTwoWay");
        _assignedTitle = _root.Q<Label>("AssignedTitle");
        _assignedHeader = _root.Q<VisualElement>("AssignedHeader");
        _assignedBody = _root.Q<VisualElement>("AssignedBody");
        _eligibleHeader = _root.Q<VisualElement>("EligibleHeader");
        _eligibleBody = _root.Q<VisualElement>("EligibleBody");

        // Resto de páginas
        _glTabEast = _root.Q<Button>("GlTabEast");
        _glTabWest = _root.Q<Button>("GlTabWest");
        _standingsBody = _root.Q<VisualElement>("GlStandingsBody");
        _resultsBody = _root.Q<VisualElement>("ResultsBody");
        _resultsHeader = _root.Q<VisualElement>("ResultsHeader");
        _resultsAffiliate = _root.Q<Label>("ResultsAffiliate");
        _leadersPts = _root.Q<VisualElement>("LeadersPts");
        _leadersReb = _root.Q<VisualElement>("LeadersReb");
        _leadersAst = _root.Q<VisualElement>("LeadersAst");
        _leadersStl = _root.Q<VisualElement>("LeadersStl");
        _leadersBlk = _root.Q<VisualElement>("LeadersBlk");
        _leadersVal = _root.Q<VisualElement>("LeadersVal");
    }

    protected override void LoadData()
    {
        base.LoadData();
        _players = _myTeam != null
            ? DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id)
            : new List<PlayerData>();

        _glTeams = DatabaseManager.Instance.GetGLeagueTeams();
        _myAffiliate = _myTeam != null
            ? _glTeams.FirstOrDefault(t => t.nba_team_id == _myTeam.id)
            : null;

        _glGames = DatabaseManager.Instance.GetAllGLeagueGames(_manager.id);
        _glStats = _season != null && _season.id > 0
            ? DatabaseManager.Instance.GetAllGLeagueSeasonStats(_season.id)
            : new List<GLeagueSeasonStat>();

        foreach (var s in Resources.LoadAll<Sprite>("Teams/GLeague/32x32"))
            _glLogos[s.name] = s;

        BuildIdentityMaps();
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _tabPlantilla?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("plantilla"); });
        _tabResultados?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("resultados"); });
        _tabClasificacion?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("clasificacion"); });
        _tabEstadisticas?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("estadisticas"); });

        if (_myAffiliate != null)
            _standingsConf = _glTeams.FirstOrDefault(t => t.id == _myAffiliate.id)?.conference ?? "East";

        _glTabEast?.RegisterCallback<ClickEvent>(_ => { PlayClick(); BuildClassification("East"); });
        _glTabWest?.RegisterCallback<ClickEvent>(_ => { PlayClick(); BuildClassification("West"); });
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[GLeague] RefreshHeader error: {ex.Message}"); }
        ShowTab(_currentTab);
    }

    // ── PESTAÑAS ─────────────────────────────────────────

    void ShowTab(string tab)
    {
        _currentTab = tab;

        SetActive(_tabPlantilla, "plantilla" == tab);
        SetActive(_tabResultados, "resultados" == tab);
        SetActive(_tabClasificacion, "clasificacion" == tab);
        SetActive(_tabEstadisticas, "estadisticas" == tab);

        SetVisible(_pagePlantilla, "plantilla" == tab);
        SetVisible(_pageResultados, "resultados" == tab);
        SetVisible(_pageClasificacion, "clasificacion" == tab);
        SetVisible(_pageEstadisticas, "estadisticas" == tab);

        switch (tab)
        {
            case "resultados": BuildResults(); break;
            case "clasificacion": BuildClassification(_standingsConf); break;
            case "estadisticas": BuildLeaders(); break;
            default:
                RefreshSummary();
                BuildAssigned();
                BuildEligible();
                break;
        }
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

    void BuildIdentityMaps()
    {
        _glNames.Clear();
        _glTeamOfPlayer.Clear();
        _glPhotos.Clear();

        var affiliateByNba = _glTeams.ToDictionary(t => t.nba_team_id, t => t.id);

        foreach (var p in _players.Where(p => p.g_league_assigned == 1))
        {
            _glNames[p.id] = $"{p.first_name} {p.last_name}";
            if (affiliateByNba.TryGetValue(p.team_id, out var glId))
                _glTeamOfPlayer[p.id] = glId;
            _glPhotos[p.id] = (p.photo, true);
        }

        var prospects = DatabaseManager.Instance.GetAllGLeaguePlayers();
        foreach (var gp in prospects)
        {
            int simId = GLeagueHelper.ProspectSimId(gp);
            _glNames[simId] = $"{gp.first_name} {gp.last_name}";
            _glTeamOfPlayer[simId] = gp.gleague_team_id;
            _glPhotos[simId] = (gp.photo ?? "", false);
        }
    }

    // ── PÁGINA PLANTILLA (contenido original) ────────────

    void RefreshSummary()
    {
        if (_summaryAssigned != null)
            _summaryAssigned.text = _players.Count(p => p.g_league_assigned == 1).ToString();
        if (_summaryTwoWay != null && _myTeam != null)
            _summaryTwoWay.text = $"{DatabaseManager.Instance.GetTwoWayCount(_myTeam.id)}/{TradeHelper.MAX_TWO_WAY}";
    }

    void BuildAssigned()
    {
        // La cabecera muestra la plantilla completa de la filial
        if (_assignedTitle != null)
            _assignedTitle.text = _myAffiliate != null
                ? $"PLANTILLA DE LOS {_myAffiliate.name.ToUpper()}"
                : "ASIGNADOS A G-LEAGUE";

        if (_assignedHeader != null)
        {
            _assignedHeader.Clear();
            _assignedHeader.Add(MakeHeaderCell("JUGADOR", "gleague-player"));
            _assignedHeader.Add(MakeHeaderCell("POS", "col-pos"));
            _assignedHeader.Add(MakeHeaderCell("EDAD", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("PJ", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("PTS", "col-stat", true));
            _assignedHeader.Add(MakeHeaderCell("REB", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("AST", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("ROB", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("TAP", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("", "gleague-action"));
        }

        if (_assignedBody == null) return;
        _assignedBody.Clear();

        // Plantilla completa de la filial: asignados NBA + prospectos, por media
        var rows = new List<(int overall, string name, Texture2D photo, string pos, int age, GLeagueSeasonStat stat, System.Action action)>();

        foreach (var p in _players.Where(p => p.g_league_assigned == 1))
        {
            var stat = _season != null ? DatabaseManager.Instance.GetGLeagueStats(p.id, _season.id) : null;
            rows.Add((p.GetCalculatedAverage(), $"{p.first_name} {p.last_name}",
                PlayerPhotoHelper.Load(p.id, p.photo), PositionCodes.GetShort(p.position), p.age, stat,
                () =>
                {
                    DatabaseManager.Instance.SetGLeagueAssignment(p, false);
                    LoadData();
                    ShowTab("plantilla");
                }));
        }

        if (_myAffiliate != null)
        {
            foreach (var gp in DatabaseManager.Instance.GetGLeaguePlayersByTeam(_myAffiliate.id))
            {
                var stat = _season != null
                    ? DatabaseManager.Instance.GetGLeagueStats(GLeagueHelper.ProspectSimId(gp), _season.id)
                    : null;
                Texture2D glPhoto = !string.IsNullOrEmpty(gp.photo)
                    ? Resources.Load<Texture2D>($"PlayerPhotos/{gp.photo}")
                    : null;
                rows.Add((gp.overall, $"{gp.first_name} {gp.last_name}", glPhoto,
                    PositionCodes.GetShort(gp.position), gp.age, stat, null));
            }
        }

        if (rows.Count == 0)
        {
            _assignedBody.Add(MakeEmpty("NO HAY JUGADORES ASIGNADOS A LA G-LEAGUE"));
            return;
        }

        foreach (var r in rows.OrderByDescending(x => x.overall))
        {
            var row = new VisualElement();
            row.AddToClassList("stats-row");
            row.AddToClassList("stats-row--my-team");

            var cell = new VisualElement();
            cell.AddToClassList("gleague-player");
            var avatar = new VisualElement();
            avatar.AddToClassList("gleague-avatar");
            if (r.photo != null)
                avatar.style.backgroundImage = new StyleBackground(r.photo);
            cell.Add(avatar);
            var nameLbl = new Label(r.name);
            nameLbl.AddToClassList("gleague-player-name");
            cell.Add(nameLbl);
            row.Add(cell);

            row.Add(MakeStatCell(r.pos, false, "col-pos"));
            row.Add(MakeStatCell(r.age.ToString(), false, "col-stat"));

            int games = r.stat?.games ?? 0;
            row.Add(MakeStatCell(games.ToString(), false, "col-stat"));
            row.Add(MakeStatCell(PerGame(r.stat?.points, games), true, "col-stat"));
            row.Add(MakeStatCell(PerGame(r.stat?.rebounds, games), false, "col-stat"));
            row.Add(MakeStatCell(PerGame(r.stat?.assists, games), false, "col-stat"));
            row.Add(MakeStatCell(PerGame(r.stat?.steals, games), false, "col-stat"));
            row.Add(MakeStatCell(PerGame(r.stat?.blocks, games), false, "col-stat"));

            if (r.action != null)
                row.Add(MakeActionCell("RECUPERAR", true, r.action));
            else
            {
                var emptyAction = new VisualElement();
                emptyAction.AddToClassList("gleague-action");
                row.Add(emptyAction);
            }

            _assignedBody.Add(row);
        }
    }

    void BuildEligible()
    {
        if (_eligibleHeader != null)
        {
            _eligibleHeader.Clear();
            _eligibleHeader.Add(MakeHeaderCell("JUGADOR", "gleague-player"));
            _eligibleHeader.Add(MakeHeaderCell("POS", "col-pos"));
            _eligibleHeader.Add(MakeHeaderCell("ROL", "col-pos"));
            _eligibleHeader.Add(MakeHeaderCell("EDAD", "col-stat"));
            _eligibleHeader.Add(MakeHeaderCell("MEDIA", "col-stat", true));
            _eligibleHeader.Add(MakeHeaderCell("POT", "col-stat"));
            _eligibleHeader.Add(MakeHeaderCell("", "gleague-action"));
        }

        if (_eligibleBody == null) return;
        _eligibleBody.Clear();

        bool enough = GLeagueHelper.HasEnoughActive(_players);
        var eligible = _players
            .Where(p => GLeagueHelper.CanAssign(p))
            .OrderByDescending(p => p.potential)
            .ToList();

        if (!enough || eligible.Count == 0)
        {
            _eligibleBody.Add(MakeEmpty(enough
                ? "NO HAY JUGADORES ELEGIBLES PARA ASIGNAR"
                : "NECESITAS AL MENOS 12 JUGADORES ACTIVOS EN LA NBA PARA ASIGNAR A LA G-LEAGUE"));
            return;
        }

        foreach (var p in eligible)
        {
            var row = new VisualElement();
            row.AddToClassList("stats-row");
            row.AddToClassList("stats-row--my-team");

            row.Add(MakePlayerCell(p));

            row.Add(MakeStatCell(PositionCodes.GetShort(p.position), false, "col-pos"));
            row.Add(MakeRoleCell(p));
            row.Add(MakeStatCell(p.age.ToString(), false, "col-stat"));
            row.Add(MakeStatCell(p.GetCalculatedAverage().ToString(), true, "col-stat"));
            row.Add(MakeStatCell(p.potential.ToString(), false, "col-stat"));

            row.Add(MakeActionCell("ASIGNAR", false, () =>
            {
                DatabaseManager.Instance.SetGLeagueAssignment(p, true);
                LoadData();
                ShowTab("plantilla");
            }));

            _eligibleBody.Add(row);
        }
    }

    // ── PÁGINA RESULTADOS (solo MI filial) ───────────────

    void BuildResults()
    {
        if (_resultsBody == null) return;
        _resultsBody.Clear();
        if (_resultsHeader != null) _resultsHeader.Clear();

        if (_resultsAffiliate != null)
            _resultsAffiliate.text = _myAffiliate != null ? $"TU FILIAL · {_myAffiliate.name.ToUpper()}" : "";

        if (_myAffiliate == null || _glGames.Count == 0)
        {
            _resultsBody.Add(MakeEmpty("LA TEMPORADA G-LEAGUE COMIENZA EN NOVIEMBRE"));
            return;
        }

        bool IsMine(GameData g) =>
            GLeagueHelper.DecodeGlTeamId(g.home_team_id) == _myAffiliate.id
            || GLeagueHelper.DecodeGlTeamId(g.away_team_id) == _myAffiliate.id;

        // Todos los partidos de mi filial (jugados y pendientes), cronológico
        var myGames = _glGames
            .Where(g => IsMine(g))
            .OrderBy(g => g.game_day)
            .ToList();

        if (myGames.Count == 0)
        {
            _resultsBody.Add(MakeEmpty("AÚN NO HAY PARTIDOS DE TU FILIAL"));
            return;
        }

        BuildResultsHeader();

        foreach (var g in myGames)
            _resultsBody.Add(MakeResultRow(g));
    }

    void BuildResultsHeader()
    {
        if (_resultsHeader == null) return;
        _resultsHeader.Clear();

        _resultsHeader.Add(MakeCell("FECHA", "gl-c-date", false, true));
        _resultsHeader.Add(MakeCell("", "gl-c-logo", false, true));
        _resultsHeader.Add(MakeCell("EQUIPO LOCAL", "gl-c-name", false, true));
        _resultsHeader.Add(MakeCell("VS", "gl-c-vs", false, true));
        _resultsHeader.Add(MakeCell("", "gl-c-logo", false, true));
        _resultsHeader.Add(MakeCell("EQUIPO VISITANTE", "gl-c-name", false, true));
        _resultsHeader.Add(MakeCell("MARCADOR", "gl-c-score", false, true));
    }

    Label MakeCell(string text, string classList, bool bold, bool header)
    {
        var lbl = new Label(text);
        foreach (var c in classList.Split(' '))
            if (!string.IsNullOrEmpty(c)) lbl.AddToClassList(c);
        if (header) lbl.AddToClassList("gl-cell--header");
        if (bold) lbl.AddToClassList("gl-cell--bold");
        return lbl;
    }

    static string FormatDate(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return isoDate;
        if (System.DateTime.TryParseExact(isoDate, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d))
            return d.ToString("dd/MM/yyyy");
        return isoDate;
    }

    VisualElement MakeResultRow(GameData g)
    {
        var row = new VisualElement();
        row.AddToClassList("gl-table-row");

        if (g.game_type == GLeaguePostSeason.TYPE_PLAYOFF) row.AddToClassList("gl-table-row--playoff");

        int homeId = GLeagueHelper.DecodeGlTeamId(g.home_team_id);
        int awayId = GLeagueHelper.DecodeGlTeamId(g.away_team_id);

        row.Add(MakeCell(FormatDate(g.game_date), "gl-c-date", false, false));

        row.Add(MakeLogo(homeId));
        row.Add(MakeTeamName(homeId));

        row.Add(MakeCell("VS", "gl-c-vs", false, false));

        row.Add(MakeLogo(awayId));
        row.Add(MakeTeamName(awayId));

        row.Add(MakeCell(g.is_played == 1 ? $"{g.home_score} - {g.away_score}" : "-", "gl-c-score", true, false));

        return row;
    }

    VisualElement MakeLogo(int gleagueTeamId)
    {
        var logo = new VisualElement();
        logo.AddToClassList("gl-c-logo");
        var team = _glTeams.FirstOrDefault(t => t.id == gleagueTeamId);
        if (team != null && _glLogos.TryGetValue(team.logo, out var sprite))
            logo.style.backgroundImage = new StyleBackground(sprite);
        return logo;
    }

    VisualElement MakeTeamName(int gleagueTeamId)
    {
        var name = new Label();
        name.AddToClassList("gl-c-name");
        var team = _glTeams.FirstOrDefault(t => t.id == gleagueTeamId);
        name.text = team?.name.ToUpper() ?? "—";
        return name;
    }

    // ── PÁGINA CLASIFICACIÓN ─────────────────────────────

    void BuildClassification(string conference)
    {
        _standingsConf = conference;

        if (_glTabEast != null) SetActive(_glTabEast, conference == "East");
        if (_glTabWest != null) SetActive(_glTabWest, conference == "West");

        if (_standingsBody == null) return;
        _standingsBody.Clear();

        var confTeams = _glTeams.Where(t => t.conference == conference).ToList();
        var table = GLeagueStandings.Compute(confTeams, _glGames);

        bool anyPlayed = table.Any(r => r.wins + r.losses > 0);
        if (!anyPlayed)
        {
            _standingsBody.Add(MakeEmpty("AÚN NO SE HA DISPUTADO NINGÚN PARTIDO DE LIGA REGULAR"));
            return;
        }

        for (int i = 0; i < table.Count; i++)
        {
            var row = table[i];
            var team = confTeams.FirstOrDefault(t => t.id == row.teamId);
            _standingsBody.Add(CreateStandingRow(i + 1, row, team));
        }
    }

    VisualElement CreateStandingRow(int rank, GLeagueStandingRow row, GLeagueTeamData team)
    {
        var elem = new VisualElement();
        elem.AddToClassList("standings-row");

        bool isMyAffiliate = _myAffiliate != null && team != null && team.id == _myAffiliate.id;
        if (isMyAffiliate) elem.AddToClassList("standings-row--my-team");
        else if (rank <= GLeaguePostSeason.TEAMS_PER_CONFERENCE) elem.AddToClassList("standings-row--playoff");
        else elem.AddToClassList("standings-row--lottery");

        int gp = row.wins + row.losses;
        float pct = gp > 0 ? (float)row.wins / gp : 0f;
        int diff = row.pf - row.pa;

        var (streakText, streakType) = CalcStreak(row.results);

        var rankLbl = new Label(rank.ToString());
        rankLbl.AddToClassList("col-rank");

        var logoElem = new VisualElement();
        logoElem.AddToClassList("col-team-logo");
        if (team != null && _glLogos.TryGetValue(team.logo, out var sprite))
            logoElem.style.backgroundImage = new StyleBackground(sprite);

        var nameLbl = new Label(team?.name.ToUpper() ?? "—");
        nameLbl.AddToClassList("col-team-name");

        var gpLbl = new Label(gp.ToString());
        gpLbl.AddToClassList("col-stat");

        var wLbl = new Label(row.wins.ToString());
        wLbl.AddToClassList("col-stat");
        wLbl.AddToClassList("col-wins");

        var lLbl = new Label(row.losses.ToString());
        lLbl.AddToClassList("col-stat");
        lLbl.AddToClassList("col-losses");

        var pctLbl = new Label(pct.ToString("F3"));
        pctLbl.AddToClassList("col-stat");

        var diffLbl = new Label(diff > 0 ? $"+{diff}" : diff.ToString());
        diffLbl.AddToClassList("col-diff");
        diffLbl.style.color = diff > 0
            ? new StyleColor(new Color(0.15f, 0.68f, 0.38f))
            : new StyleColor(new Color(0.75f, 0.22f, 0.17f));

        var streakLbl = new Label(streakText);
        streakLbl.AddToClassList("col-streak");
        streakLbl.AddToClassList(streakType == "win" ? "streak-win" :
                                streakType == "loss" ? "streak-loss" : "streak-none");

        elem.Add(rankLbl);
        elem.Add(logoElem);
        elem.Add(nameLbl);
        elem.Add(gpLbl);
        elem.Add(wLbl);
        elem.Add(lLbl);
        elem.Add(pctLbl);
        elem.Add(diffLbl);
        elem.Add(streakLbl);

        return elem;
    }

    static (string text, string type) CalcStreak(List<bool> results)
    {
        if (results == null || results.Count == 0) return ("-", "none");
        bool last = results[results.Count - 1];
        int count = 0;
        for (int i = results.Count - 1; i >= 0; i--)
        {
            if (results[i] == last) count++;
            else break;
        }
        return last ? ($"{count}V", "win") : ($"{count}D", "loss");
    }

    // ── PÁGINA ESTADÍSTICAS (líderes) ────────────────────

    void BuildLeaders()
    {
        BuildLeaderPanel(_leadersPts, s => s.points);
        BuildLeaderPanel(_leadersReb, s => s.rebounds);
        BuildLeaderPanel(_leadersAst, s => s.assists);
        BuildLeaderPanel(_leadersStl, s => s.steals);
        BuildLeaderPanel(_leadersBlk, s => s.blocks);
        BuildLeaderPanel(_leadersVal, s => s.rating);
    }

    void BuildLeaderPanel(VisualElement body, System.Func<GLeagueSeasonStat, int> selector)
    {
        if (body == null) return;
        body.Clear();

        var ranked = _glStats
            .Where(s => s.games > 0)
            .Select(s => new { stat = s, avg = selector(s) / (float)s.games })
            .OrderByDescending(x => x.avg)
            .Take(8)
            .ToList();

        if (ranked.Count == 0)
        {
            body.Add(MakeEmpty("SIN DATOS TODAVÍA"));
            return;
        }

        for (int i = 0; i < ranked.Count; i++)
            body.Add(MakeLeaderRow(i + 1, ranked[i].stat, ranked[i].avg));
    }

    VisualElement MakeLeaderRow(int rank, GLeagueSeasonStat stat, float avg)
    {
        var row = new VisualElement();
        row.AddToClassList("gl-leader-row");

        var rankLbl = new Label(rank.ToString());
        rankLbl.AddToClassList("gl-leader-rank");
        row.Add(rankLbl);

        var avatar = new VisualElement();
        avatar.AddToClassList("gl-leader-avatar");
        if (_glPhotos.TryGetValue(stat.player_id, out var photoInfo) && (photoInfo.isReal || !string.IsNullOrEmpty(photoInfo.photo)))
        {
            var tex = PlayerPhotoHelper.Load(stat.player_id, photoInfo.photo);
            if (tex != null)
                avatar.style.backgroundImage = new StyleBackground(tex);
        }
        row.Add(avatar);

        var info = new VisualElement();
        info.AddToClassList("gl-leader-info");

        var nameLbl = new Label(_glNames.TryGetValue(stat.player_id, out var n) ? n : "—");
        nameLbl.AddToClassList("gl-leader-name");
        info.Add(nameLbl);

        string teamName = "";
        if (_glTeamOfPlayer.TryGetValue(stat.player_id, out var glId))
            teamName = _glTeams.FirstOrDefault(t => t.id == glId)?.name.ToUpper() ?? "";
        var teamLbl = new Label(teamName);
        teamLbl.AddToClassList("gl-leader-team");
        info.Add(teamLbl);

        var gamesLbl = new Label($"{stat.games} partido{(stat.games == 1 ? "" : "s")}");
        gamesLbl.AddToClassList("gl-leader-games");
        info.Add(gamesLbl);

        row.Add(info);

        var valueLbl = new Label(avg.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        valueLbl.AddToClassList("gl-leader-value");
        row.Add(valueLbl);

        return row;
    }

    // ── HELPERS DE CELDA ─────────────────────────────────

    Label MakeHeaderCell(string text, string baseClass, bool bold = false)
    {
        var lbl = new Label();
        lbl.AddToClassList(baseClass);
        lbl.AddToClassList("col-stat--header");
        if (bold) lbl.AddToClassList("col-stat--bold");
        lbl.text = text;
        return lbl;
    }

    Label MakeStatCell(string value, bool bold, string baseClass)
    {
        var lbl = new Label();
        lbl.AddToClassList(baseClass);
        if (bold) lbl.AddToClassList("col-stat--bold");
        lbl.text = value;
        return lbl;
    }

    VisualElement MakePlayerCell(PlayerData p)
    {
        var cell = new VisualElement();
        cell.AddToClassList("gleague-player");

        var avatar = new VisualElement();
        avatar.AddToClassList("gleague-avatar");
        Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
        if (tex != null)
            avatar.style.backgroundImage = new StyleBackground(tex);
        cell.Add(avatar);

        var nameLbl = new Label();
        nameLbl.AddToClassList("gleague-player-name");
        nameLbl.text = $"{p.first_name} {p.last_name}";
        cell.Add(nameLbl);

        return cell;
    }

    VisualElement MakeRoleCell(PlayerData p)
    {
        var cell = new VisualElement();
        cell.AddToClassList("gleague-role");

        var icon = new VisualElement();
        icon.AddToClassList("gleague-role-icon");
        string iconName = p.role switch
        {
            PlayerRole.Estrella => "rol_estrella",
            PlayerRole.Titular => "rol_titular",
            PlayerRole.Banquillo => "rol_banquillo",
            _ => "rol_ultimoRecurso"
        };
        var tex = Resources.Load<Texture2D>($"Icons/{iconName}");
        icon.style.backgroundImage = tex != null ? new StyleBackground(tex) : StyleKeyword.None;
        icon.tooltip = p.role switch
        {
            PlayerRole.Estrella => "Estrella",
            PlayerRole.Titular => "Titular",
            PlayerRole.Banquillo => "Banquillo",
            _ => "Último recurso"
        };
        cell.Add(icon);

        return cell;
    }

    VisualElement MakeActionCell(string text, bool isOn, System.Action onClick)
    {
        var cell = new VisualElement();
        cell.AddToClassList("gleague-action");

        var btn = new Button();
        btn.AddToClassList("gleague-action-btn");
        if (isOn) btn.AddToClassList("gleague-action-btn--on");
        btn.text = text;
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            onClick();
        });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(btn);
        cell.Add(btn);

        return cell;
    }

    VisualElement MakeEmpty(string message)
    {
        var empty = new VisualElement();
        empty.AddToClassList("stats-empty");
        var lbl = new Label();
        lbl.AddToClassList("stats-empty-label");
        lbl.text = message;
        empty.Add(lbl);
        return empty;
    }

    static string PerGame(int? total, int games)
    {
        if (games <= 0 || total == null) return "—";
        return (total.Value / (float)games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }
}
