using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class PalmaresController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private Button _tabEquipos;
    private Button _tabJugadores;
    private Button _tabQuintetos;

    private VisualElement _tabContentEquipos;
    private VisualElement _tabContentJugadores;
    private VisualElement _tabContentQuintetos;

    // Equipos
    private VisualElement _championsBody;
    private Label _noChampionsText;
    private VisualElement _confChampionsBody;
    private VisualElement _divChampionsBody;

    // Jugadores
    private VisualElement _mvpBody;
    private VisualElement _rookieBody;
    private VisualElement _defenderBody;
    private VisualElement _sixthManBody;
    private VisualElement _improvedBody;

    // Quintetos
    private VisualElement _firstTeamBody;
    private VisualElement _secondTeamBody;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;
    private List<SeasonRecord> _seasonRecords;

    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        CacheReferences();
        LoadSidebarIcons();
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");

        // Tabs
        _tabEquipos = _root.Q<Button>("TabEquipos");
        _tabJugadores = _root.Q<Button>("TabJugadores");
        _tabQuintetos = _root.Q<Button>("TabQuintetos");

        _tabContentEquipos = _root.Q<VisualElement>("TabContentEquipos");
        _tabContentJugadores = _root.Q<VisualElement>("TabContentJugadores");
        _tabContentQuintetos = _root.Q<VisualElement>("TabContentQuintetos");

        // Equipos
        _championsBody = _root.Q<VisualElement>("ChampionsBody");
        _noChampionsText = _root.Q<Label>("NoChampionsText");
        _confChampionsBody = _root.Q<VisualElement>("ConfChampionsBody");
        _divChampionsBody = _root.Q<VisualElement>("DivChampionsBody");

        // Jugadores
        _mvpBody = _root.Q<VisualElement>("MVPBody");
        _rookieBody = _root.Q<VisualElement>("RookieBody");
        _defenderBody = _root.Q<VisualElement>("DefenderBody");
        _sixthManBody = _root.Q<VisualElement>("SixthManBody");
        _improvedBody = _root.Q<VisualElement>("ImprovedBody");

        // Quintetos
        _firstTeamBody = _root.Q<VisualElement>("FirstTeamBody");
        _secondTeamBody = _root.Q<VisualElement>("SecondTeamBody");
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _seasonRecords = DatabaseManager.Instance.GetAllSeasonRecords(_season.id);
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();

        _tabEquipos?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("equipos"); });
        _tabJugadores?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("jugadores"); });
        _tabQuintetos?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("quintetos"); });

        _btnAction?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
    }

    void RegisterNavButtons()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Roster); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Records); });
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Market); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Finances); });
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<Button>("NavConfig")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });
    }

    void Refresh()
    {
        RefreshHeader();
        ShowTab("equipos");
    }

    void ShowTab(string tab)
    {
        _tabEquipos.RemoveFromClassList("palmares-tab--active");
        _tabJugadores.RemoveFromClassList("palmares-tab--active");
        _tabQuintetos.RemoveFromClassList("palmares-tab--active");

        _tabContentEquipos.style.display = DisplayStyle.None;
        _tabContentJugadores.style.display = DisplayStyle.None;
        _tabContentQuintetos.style.display = DisplayStyle.None;

        switch (tab)
        {
            case "equipos":
                _tabEquipos.AddToClassList("palmares-tab--active");
                _tabContentEquipos.style.display = DisplayStyle.Flex;
                BuildChampions();
                BuildConfChampions();
                BuildDivChampions();
                break;
            case "jugadores":
                _tabJugadores.AddToClassList("palmares-tab--active");
                _tabContentJugadores.style.display = DisplayStyle.Flex;
                BuildPlayerAwards();
                break;
            case "quintetos":
                _tabQuintetos.AddToClassList("palmares-tab--active");
                _tabContentQuintetos.style.display = DisplayStyle.Flex;
                BuildQuintetos();
                break;
        }
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites64.TryGetValue(_myTeam.logo, out var sprite))
            _root.Q<VisualElement>("HeaderTeamLogo").style.backgroundImage = new StyleBackground(sprite);

        _root.Q<Label>("HeaderTeamName").text = _myTeam.name.ToUpper();
        _root.Q<Label>("HeaderManagerName").text = $"Manager: {_manager.name}";
        _root.Q<Label>("HeaderBudget").text = $"${_myTeam.budget / 1_000_000}M";

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long totalPayroll = players.Sum(p => p.salary);
        _root.Q<Label>("HeaderPayroll").text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? 155_000_000;
        long margin = salaryCap - totalPayroll;
        var marginLbl = _root.Q<Label>("HeaderMargin");
        marginLbl.text = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        marginLbl.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) marginLbl.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _root.Q<Label>("HeaderSeason").text = $"Temporada {_season.year_start}-{_season.year_end}";
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetNextGameDateString(_manager.id, _myTeam.id);
        }

        _btnAction.text = "DASHBOARD";
    }

    // ══ EQUIPOS ══════════════════════════════════════════

    void BuildChampions()
    {
        _championsBody.Clear();

        if (_seasonRecords.Count == 0)
        {
            _noChampionsText.style.display = DisplayStyle.Flex;
            return;
        }

        _noChampionsText.style.display = DisplayStyle.None;

        foreach (var record in _seasonRecords)
        {
            var champion = _allTeams.Find(t => t.id == record.champion_id);
            var seasonStr = GetSeasonString(record.season_id);

            var row = new VisualElement();
            row.AddToClassList("palmares-row");
            if (champion != null && champion.id == _myTeam.id)
                row.AddToClassList("palmares-row--my-team");

            var seasonLbl = new Label();
            seasonLbl.AddToClassList("palmares-season");
            seasonLbl.text = seasonStr;

            var trophy = new Label();
            trophy.AddToClassList("palmares-trophy");
            trophy.text = "🏆";

            var logoElem = new VisualElement();
            logoElem.AddToClassList("palmares-team-logo");
            if (champion != null && _logoSprites.TryGetValue(champion.logo, out var sp))
                logoElem.style.backgroundImage = new StyleBackground(sp);

            var nameLbl = new Label();
            nameLbl.AddToClassList("palmares-team-name");
            nameLbl.text = champion?.name.ToUpper() ?? "???";

            var resultLbl = new Label();
            resultLbl.AddToClassList("palmares-result");
            resultLbl.text = record.finals_result;

            row.Add(seasonLbl);
            row.Add(trophy);
            row.Add(logoElem);
            row.Add(nameLbl);
            row.Add(resultLbl);

            _championsBody.Add(row);
        }
    }

    void BuildConfChampions()
    {
        _confChampionsBody.Clear();

        foreach (var record in _seasonRecords)
        {
            var seasonStr = GetSeasonString(record.season_id);

            // East
            if (record.east_champion_id > 0)
            {
                var team = _allTeams.Find(t => t.id == record.east_champion_id);
                _confChampionsBody.Add(CreateTeamAwardRow(seasonStr, "ESTE", team));
            }

            // West
            if (record.west_champion_id > 0)
            {
                var team = _allTeams.Find(t => t.id == record.west_champion_id);
                _confChampionsBody.Add(CreateTeamAwardRow(seasonStr, "OESTE", team));
            }
        }
    }

    void BuildDivChampions()
    {
        _divChampionsBody.Clear();

        foreach (var record in _seasonRecords)
        {
            var seasonStr = GetSeasonString(record.season_id);

            var divIds = new[] {
                record.div1_champion_id, record.div2_champion_id,
                record.div3_champion_id, record.div4_champion_id,
                record.div5_champion_id, record.div6_champion_id
            };

            foreach (var divId in divIds)
            {
                if (divId > 0)
                {
                    var team = _allTeams.Find(t => t.id == divId);
                    var divName = team?.division ?? "";
                    _divChampionsBody.Add(CreateTeamAwardRow(seasonStr, divName, team));
                }
            }
        }
    }

    VisualElement CreateTeamAwardRow(string season, string label, TeamData team)
    {
        var row = new VisualElement();
        row.AddToClassList("palmares-row");
        if (team != null && team.id == _myTeam.id)
            row.AddToClassList("palmares-row--my-team");

        var seasonLbl = new Label();
        seasonLbl.AddToClassList("palmares-season");
        seasonLbl.text = season;

        var labelLbl = new Label();
        labelLbl.AddToClassList("award-label");
        labelLbl.text = label;

        var logoElem = new VisualElement();
        logoElem.AddToClassList("palmares-team-logo");
        if (team != null && _logoSprites.TryGetValue(team.logo, out var sp))
            logoElem.style.backgroundImage = new StyleBackground(sp);

        var nameLbl = new Label();
        nameLbl.AddToClassList("palmares-team-name");
        nameLbl.text = team?.name.ToUpper() ?? "???";

        row.Add(seasonLbl);
        row.Add(labelLbl);
        row.Add(logoElem);
        row.Add(nameLbl);

        return row;
    }

    // ══ JUGADORES ══════════════════════════════════════════

    void BuildPlayerAwards()
    {
        BuildAwardList(_mvpBody, "MVP TEMPORADA", _seasonRecords.Select(r => r.season_mvp_id).ToList());
        BuildAwardList(_rookieBody, "ROOKIE DEL AÑO", _seasonRecords.Select(r => r.rookie_of_year_id).ToList());
        BuildAwardList(_defenderBody, "MEJOR DEFENSOR", _seasonRecords.Select(r => r.best_defender_id).ToList());
        BuildAwardList(_sixthManBody, "MEJOR SEXTO HOMBRE", _seasonRecords.Select(r => r.sixth_man_id).ToList());
        BuildAwardList(_improvedBody, "MÁS MEJORADO", _seasonRecords.Select(r => r.most_improved_id).ToList());
    }

    void BuildAwardList(VisualElement body, string awardType, List<int> playerIds)
    {
        body.Clear();

        for (int i = 0; i < _seasonRecords.Count && i < playerIds.Count; i++)
        {
            var record = _seasonRecords[i];
            var playerId = playerIds[i];
            var seasonStr = GetSeasonString(record.season_id);

            var row = new VisualElement();
            row.AddToClassList("palmares-row");

            var seasonLbl = new Label();
            seasonLbl.AddToClassList("palmares-season");
            seasonLbl.text = seasonStr;

            var awardLbl = new Label();
            awardLbl.AddToClassList("award-label");
            awardLbl.text = awardType;

            var player = DatabaseManager.Instance.GetPlayerById(playerId);

            var winnerLbl = new Label();
            winnerLbl.AddToClassList("award-winner");
            winnerLbl.text = player != null ? $"{player.first_name} {player.last_name}" : "—";

            var teamLbl = new Label();
            teamLbl.AddToClassList("award-team");
            if (player != null)
            {
                var team = _allTeams.Find(t => t.id == player.team_id);
                teamLbl.text = team?.abbreviation ?? "";
            }

            row.Add(seasonLbl);
            row.Add(awardLbl);
            row.Add(winnerLbl);
            row.Add(teamLbl);

            body.Add(row);
        }
    }

    // ══ QUINTETOS ══════════════════════════════════════════

    void BuildQuintetos()
    {
        _firstTeamBody.Clear();
        _secondTeamBody.Clear();

        foreach (var record in _seasonRecords)
        {
            var seasonStr = GetSeasonString(record.season_id);

            // First team
            var firstTeamIds = new[] {
                record.first_team_pg, record.first_team_sg,
                record.first_team_sf, record.first_team_pf, record.first_team_c
            };
            var positions = new[] { "PG", "SG", "SF", "PF", "C" };

            for (int i = 0; i < 5; i++)
            {
                if (firstTeamIds[i] > 0)
                {
                    var player = DatabaseManager.Instance.GetPlayerById(firstTeamIds[i]);
                    _firstTeamBody.Add(CreateQuintetoRow(seasonStr, positions[i], player));
                }
            }

            // Second team
            var secondTeamIds = new[] {
                record.second_team_pg, record.second_team_sg,
                record.second_team_sf, record.second_team_pf, record.second_team_c
            };

            for (int i = 0; i < 5; i++)
            {
                if (secondTeamIds[i] > 0)
                {
                    var player = DatabaseManager.Instance.GetPlayerById(secondTeamIds[i]);
                    _secondTeamBody.Add(CreateQuintetoRow(seasonStr, positions[i], player));
                }
            }
        }
    }

    VisualElement CreateQuintetoRow(string season, string pos, PlayerData player)
    {
        var row = new VisualElement();
        row.AddToClassList("quinteto-row");

        var posLbl = new Label();
        posLbl.AddToClassList("quinteto-pos");
        posLbl.text = pos;

        var logoElem = new VisualElement();
        logoElem.AddToClassList("quinteto-player-logo");

        var nameLbl = new Label();
        nameLbl.AddToClassList("quinteto-player-name");
        nameLbl.text = player != null ? $"{player.first_name} {player.last_name}" : "—";

        var teamLbl = new Label();
        teamLbl.AddToClassList("quinteto-player-team");
        if (player != null)
        {
            var team = _allTeams.Find(t => t.id == player.team_id);
            teamLbl.text = team?.abbreviation ?? "";
        }

        row.Add(posLbl);
        row.Add(logoElem);
        row.Add(nameLbl);
        row.Add(teamLbl);

        return row;
    }

    string GetSeasonString(int seasonId)
    {
        // For now, try to find from records or return generic
        return $"{2025}-{2026}";
    }

    void LoadSidebarIcons()
    {
        var iconMap = new System.Collections.Generic.Dictionary<string, string>
        {
            {"NavDashboardIcon", "inicio"},
            {"NavRosterIcon", "plantilla"},
            {"NavCalendarIcon", "calendario"},
            {"NavStandingsIcon", "clasificacion"},
            {"NavPalmaresIcon", "palmares"},
            {"NavResultsIcon", "resultados"},
            {"NavPlayoffsIcon", "playoff"},
            {"NavStatsIcon", "estadisticas"},
            {"NavRecordsIcon", "records"},
            {"NavMarketIcon", "mercado"},
            {"NavFinancesIcon", "finanzas"},
            {"NavSponsorsIcon", "patrocinador"},
            {"NavTVIcon", "television"},
            {"NavArenaIcon", "pabellon"},
            {"NavMessagesIcon", "mensajes"},
            {"NavConfigIcon", "configuracion"}
        };

        foreach (var kv in iconMap)
        {
            var iconElem = _root.Q<VisualElement>(kv.Key);
            if (iconElem == null) continue;
            var tex = Resources.Load<Texture2D>($"Icons/{kv.Value}");
            if (tex != null)
                iconElem.style.backgroundImage = new StyleBackground(tex);
        }
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
