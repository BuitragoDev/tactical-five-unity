using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;

public class NewSeasonController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerSeason;
    private Label _headerDate;

    // Content
    private Label _gameModeTag;
    private VisualElement _teamSelection;
    private Label _noteText;
    private Button _btnStartSeason;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;

    private Dictionary<string, Sprite> _logo52;
    private int _selectedTeamId;
    private List<TeamData> _availableTeams;

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
        LoadData();
    }

    void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _gameModeTag = _root.Q<Label>("GameModeTag");
        _teamSelection = _root.Q<VisualElement>("TeamSelection");
        _noteText = _root.Q<Label>("NoteText");
        _btnStartSeason = _root.Q<Button>("BtnStartSeason");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        if (_myTeam == null) return;
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        if (_season == null) return;

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        _logo52 = new Dictionary<string, Sprite>();
        foreach (var s in logos) _logo52[s.name] = s;

        RefreshHeader();
        RefreshContent();

        _btnStartSeason.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnStartSeason(); });
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos64) logoDict[s.name] = s;
        if (logoDict.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

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
        string modeName = _season.game_mode == "promanager" ? "ProManager" : "Manager";
        _gameModeTag.text = modeName;

        LoadAvailableTeams();
        BuildTeamCards();

        if (_season.game_mode == "promanager")
            _noteText.text = "Selecciona tu equipo actual o uno de los 3 equipos disponibles para la nueva temporada.";
        else
            _noteText.text = "Continuarás con tu equipo actual en la nueva temporada.";
    }

    void LoadAvailableTeams()
    {
        _availableTeams = new List<TeamData>();
        _selectedTeamId = _myTeam.id;

        if (_season.game_mode == "manager")
        {
            _availableTeams.Add(_myTeam);
            return;
        }

        // ProManager: current team + 3 random from bottom 10 (excluding current)
        var standings = GetTeamStandings();
        var bottom10 = standings.OrderBy(s => s.wins).Take(10).ToList();
        bottom10 = bottom10.Where(s => s.teamId != _myTeam.id).ToList();

        System.Random rng = new System.Random();
        var random3 = bottom10.OrderBy(_ => rng.Next()).Take(3).ToList();

        _availableTeams.Add(_myTeam);
        foreach (var st in random3)
        {
            var team = DatabaseManager.Instance.GetTeamById(st.teamId);
            if (team != null)
                _availableTeams.Add(team);
        }
    }

    List<(int teamId, int wins, int losses)> GetTeamStandings()
    {
        var games = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        var teams = DatabaseManager.Instance.GetAllTeams();
        var standings = teams.ToDictionary(t => t.id, t => (teamId: t.id, wins: 0, losses: 0));

        foreach (var g in games)
        {
            if (standings.ContainsKey(g.home_team_id))
            {
                var h = standings[g.home_team_id];
                if (g.home_score > g.away_score) h.wins++;
                else h.losses++;
                standings[g.home_team_id] = h;
            }
            if (standings.ContainsKey(g.away_team_id))
            {
                var a = standings[g.away_team_id];
                if (g.away_score > g.home_score) a.wins++;
                else a.losses++;
                standings[g.away_team_id] = a;
            }
        }

        var rows = standings.Values.ToList();
        rows.Sort((a, b) =>
        {
            float pctA = a.wins + a.losses > 0 ? (float)a.wins / (a.wins + a.losses) : 0;
            float pctB = b.wins + b.losses > 0 ? (float)b.wins / (b.wins + b.losses) : 0;
            if (pctB != pctA) return pctB.CompareTo(pctA);
            if (a.losses != b.losses) return a.losses.CompareTo(b.losses);
            return b.wins.CompareTo(a.wins);
        });

        return rows;
    }

    void BuildTeamCards()
    {
        _teamSelection.Clear();

        foreach (var team in _availableTeams)
        {
            var card = new VisualElement();
            card.AddToClassList("team-select-card");
            if (team.id == _selectedTeamId)
                card.AddToClassList("selected");

            var logo = new VisualElement();
            logo.AddToClassList("team-select-logo");
            if (_logo52.TryGetValue(team.logo, out var sprite))
                logo.style.backgroundImage = new StyleBackground(sprite);
            card.Add(logo);

            var info = new VisualElement();
            info.AddToClassList("team-select-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("team-select-name");
            nameLbl.text = team.name;
            info.Add(nameLbl);

            string confName = team.conference == "East" ? "Conferencia Este" : "Conferencia Oeste";
            string detail = $"{team.city} · {confName}";
            if (team.id == _myTeam.id) detail += " · TU EQUIPO";
            var detailLbl = new Label();
            detailLbl.AddToClassList("team-select-detail");
            detailLbl.text = detail;
            info.Add(detailLbl);

            card.Add(info);

            if (team.id == _selectedTeamId)
            {
                var badge = new Label();
                badge.AddToClassList("team-select-badge");
                badge.text = "SELECCIONADO";
                card.Add(badge);
            }

            int capturedId = team.id;
            card.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectTeam(capturedId); });

            _teamSelection.Add(card);
        }
    }

    void SelectTeam(int teamId)
    {
        _selectedTeamId = teamId;
        // Rebuild cards to reflect selection
        BuildTeamCards();
    }

    void OnStartSeason()
    {
        _btnStartSeason.SetEnabled(false);
        _btnStartSeason.text = "INICIANDO...";

        int newTeamId = _selectedTeamId;

        // Update manager team if changed
        if (_manager.team_id != newTeamId)
        {
            _manager.team_id = newTeamId;
            DatabaseManager.Instance.SaveManager(_manager);
        }

        // Execute the full new season logic
        DatabaseManager.Instance.StartNewSeason(
            _season.id,
            newTeamId,
            _season.game_mode,
            _manager.id
        );

        // Navigate to Dashboard
        ScreenManager.Instance.GoTo(GameScreen.Dashboard);
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
