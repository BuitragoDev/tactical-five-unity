using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class PreseasonController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // Header
    private Button _btnBack;
    private Button _btnContinue;

    // Info bar
    private VisualElement _infoBarLogo;
    private Label _infoBarTeam;
    private Label _infoBarCount;

    // Slots
    private VisualElement _slotsRow;

    // Bottom
    private Button _btnHome;
    private Button _btnAway;
    private VisualElement _teamsGrid;

    // Estado
    private TeamData _myTeam;
    private ManagerData _manager;
    private List<TeamData> _allTeams;
    private List<GameData> _games = new();
    private bool _isHome = true;

    private const int MAX_GAMES = 4;

    // Sprites
    private Dictionary<string, Sprite> _logoSprites = new();

    // Fechas en septiembre
    private readonly string[] _dates = {
        "05 SEP 2025", "08 SEP 2025",
        "11 SEP 2025", "14 SEP 2025"
    };
    private readonly string[] _datesDb = {
        "2025-09-05", "2025-09-08",
        "2025-09-11", "2025-09-14"
    };

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _root.style.position = Position.Absolute;
        _root.style.left = 0;
        _root.style.right = 0;
        _root.style.top = 0;
        _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        CacheReferences();
        LoadData();
        RegisterCallbacks();
        BuildTeamsGrid();
        RefreshSlots();
        UpdateInfoBar();
        UpdateContinueButton();
    }

    void CacheReferences()
    {
        _btnBack = _root.Q<Button>("BtnBack");
        _btnContinue = _root.Q<Button>("BtnContinue");
        _infoBarLogo = _root.Q<VisualElement>("InfoBarLogo");
        _infoBarTeam = _root.Q<Label>("InfoBarTeam");
        _infoBarCount = _root.Q<Label>("InfoBarCount");
        _slotsRow = _root.Q<VisualElement>("SlotsRow");
        _btnHome = _root.Q<Button>("BtnHome");
        _btnAway = _root.Q<Button>("BtnAway");
        _teamsGrid = _root.Q<VisualElement>("TeamsGrid");
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos/80x80/");
        foreach (var s in logos)
            _logoSprites[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _allTeams = DatabaseManager.Instance.GetAllTeams();
    }

    void RegisterCallbacks()
    {
        _btnBack?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.SelectTeam));
        _btnContinue?.RegisterCallback<ClickEvent>(_ => OnContinue());
        _btnHome?.RegisterCallback<ClickEvent>(_ => SetLocation(true));
        _btnAway?.RegisterCallback<ClickEvent>(_ => SetLocation(false));

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnBack);
            CursorManager.Instance.RegisterHandCursor(_btnContinue);
            CursorManager.Instance.RegisterHandCursor(_btnHome);
            CursorManager.Instance.RegisterHandCursor(_btnAway);
        }
    }

    // ── INFO BAR ──────────────────────────────────────────

    void UpdateInfoBar()
    {
        if (_myTeam == null) return;
        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _infoBarLogo.style.backgroundImage = new StyleBackground(sprite);
        _infoBarTeam.text = _myTeam.name.ToUpper();
        _infoBarCount.text = $"{_games.Count} / {MAX_GAMES}";
    }

    // ── LOCATION TOGGLE ───────────────────────────────────

    void SetLocation(bool isHome)
    {
        _isHome = isHome;
        if (isHome)
        {
            _btnHome.AddToClassList("toggle-btn--active");
            _btnAway.RemoveFromClassList("toggle-btn--active");
        }
        else
        {
            _btnAway.AddToClassList("toggle-btn--active");
            _btnHome.RemoveFromClassList("toggle-btn--active");
        }
    }

    // ── SLOTS ─────────────────────────────────────────────

    void RefreshSlots()
    {
        _slotsRow.Clear();

        for (int i = 0; i < MAX_GAMES; i++)
        {
            bool filled = i < _games.Count;
            _slotsRow.Add(filled
                ? CreateFilledSlot(i, _games[i])
                : CreateEmptySlot(i));
        }
    }

    VisualElement CreateEmptySlot(int index)
    {
        var slot = new VisualElement();
        slot.AddToClassList("game-slot");
        slot.AddToClassList("game-slot--empty");

        var num = new Label();
        num.AddToClassList("game-slot-number");
        num.text = $"PARTIDO {index + 1}";

        var empty = new Label();
        empty.AddToClassList("game-slot-empty-text");
        empty.text = "SIN RIVAL";

        slot.Add(num);
        slot.Add(empty);
        return slot;
    }

    VisualElement CreateFilledSlot(int index, GameData game)
    {
        var rivalId = game.is_home == 1 ? game.away_team_id : game.home_team_id;
        var rival = _allTeams.Find(t => t.id == rivalId);

        var slot = new VisualElement();
        slot.AddToClassList("game-slot");
        slot.AddToClassList("game-slot--filled");

        // Número
        var num = new Label();
        num.AddToClassList("game-slot-number");
        num.AddToClassList("game-slot-number--filled");
        num.text = $"PARTIDO {index + 1}";

        // Logo rival
        var logo = new VisualElement();
        logo.AddToClassList("game-slot-logo");
        if (rival != null && _logoSprites.TryGetValue(rival.logo, out var sprite))
            logo.style.backgroundImage = new StyleBackground(sprite);

        // Nombre rival
        var name = new Label();
        name.AddToClassList("game-slot-rival");
        name.text = rival?.name.ToUpper() ?? "???";

        // Badge local/visitante
        var location = new Label();
        location.AddToClassList("game-slot-location");
        if (game.is_home == 1)
        {
            location.AddToClassList("game-slot-location--home");
            location.text = "LOCAL";
        }
        else
        {
            location.AddToClassList("game-slot-location--away");
            location.text = "VISITANTE";
        }

        // Fecha
        var date = new Label();
        date.AddToClassList("game-slot-date");
        date.text = _dates[index];

        // Botón quitar
        var btnRemove = new Button();
        btnRemove.AddToClassList("btn-remove-slot");
        btnRemove.text = "✕";
        int captured = index;
        btnRemove.RegisterCallback<ClickEvent>(_ => RemoveGame(captured));
        if (CursorManager.Instance != null)
        {
            btnRemove.RegisterCallback<MouseEnterEvent>(_ =>
                CursorManager.Instance.SetHandCursor());
            btnRemove.RegisterCallback<MouseLeaveEvent>(_ =>
                CursorManager.Instance.SetDefaultCursor());
        }

        slot.Add(num);
        slot.Add(logo);
        slot.Add(name);
        slot.Add(location);
        slot.Add(date);
        slot.Add(btnRemove);

        return slot;
    }

    // ── TEAMS GRID ────────────────────────────────────────

    void BuildTeamsGrid()
    {
        _teamsGrid.Clear();

        // Configurar el contenedor del ScrollView
        var scroll = _root.Q<ScrollView>("TeamsScroll");
        if (scroll != null)
        {
            scroll.contentContainer.style.flexDirection = FlexDirection.Row;
            scroll.contentContainer.style.flexWrap = Wrap.Wrap;
            scroll.contentContainer.style.alignContent = Align.FlexStart;
        }

        foreach (var team in _allTeams)
        {
            if (team.id == _myTeam.id) continue;
            _teamsGrid.Add(CreateTeamItem(team));
        }
    }

    VisualElement CreateTeamItem(TeamData team)
    {
        var item = new VisualElement();
        item.AddToClassList("team-item");
        item.name = $"team_{team.id}";

        var logo = new VisualElement();
        logo.AddToClassList("team-logo");
        if (_logoSprites.TryGetValue(team.logo, out var sprite))
            logo.style.backgroundImage = new StyleBackground(sprite);

        var overall = new Label();
        overall.AddToClassList("team-overall");
        overall.text = $"MED {team.overall}";

        item.Add(logo);
        item.Add(overall);

        item.RegisterCallback<ClickEvent>(_ => OnTeamSelected(team));

        if (CursorManager.Instance != null)
        {
            item.RegisterCallback<MouseEnterEvent>(_ =>
                CursorManager.Instance.SetHandCursor());
            item.RegisterCallback<MouseLeaveEvent>(_ =>
                CursorManager.Instance.SetDefaultCursor());
        }

        return item;
    }

    void UpdateTeamsGridAvailability()
    {
        var usedIds = _games.Select(g =>
            g.is_home == 1 ? g.away_team_id : g.home_team_id).ToHashSet();

        foreach (var team in _allTeams)
        {
            if (team.id == _myTeam.id) continue;
            var item = _teamsGrid.Q<VisualElement>($"team_{team.id}");
            if (item == null) continue;

            if (usedIds.Contains(team.id) || _games.Count >= MAX_GAMES)
                item.AddToClassList("team-item--used");
            else
                item.RemoveFromClassList("team-item--used");
        }
    }

    // ── ACCIONES ──────────────────────────────────────────

    void OnTeamSelected(TeamData rival)
    {
        if (_games.Count >= MAX_GAMES) return;

        // Comprobar que no esté ya usado
        var usedIds = _games.Select(g =>
            g.is_home == 1 ? g.away_team_id : g.home_team_id).ToHashSet();
        if (usedIds.Contains(rival.id)) return;

        int slotIndex = _games.Count;

        var game = new GameData
        {
            manager_id = _manager.id,
            season_id = 0,
            game_day = -(slotIndex + 1),
            home_team_id = _isHome ? _myTeam.id : rival.id,
            away_team_id = _isHome ? rival.id : _myTeam.id,
            game_date = _datesDb[slotIndex],
            is_played = 0,
            game_type = "preseason",
            series_label = "",
            home_score = 0,
            away_score = 0,
            is_home = _isHome ? 1 : 0
        };

        _games.Add(game);
        RefreshSlots();
        UpdateTeamsGridAvailability();
        UpdateInfoBar();
        UpdateContinueButton();
    }

    void RemoveGame(int index)
    {
        if (index < 0 || index >= _games.Count) return;
        _games.RemoveAt(index);
        RefreshSlots();
        UpdateTeamsGridAvailability();
        UpdateInfoBar();
        UpdateContinueButton();
    }

    void UpdateContinueButton()
    {
        _btnContinue.SetEnabled(true);
    }

    void OnContinue()
    {
        // 1. Crear temporada si no existe
        var season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        if (season == null)
            season = DatabaseManager.Instance.CreateSeason(
                _manager.id,
                ScreenManager.Instance.CurrentMode == GameMode.ProManager
                    ? "promanager" : "manager"
            );

        // 2. Guardar amistosos si los hay
        if (_games.Count > 0)
        {
            foreach (var g in _games)
                g.season_id = season.id;
            DatabaseManager.Instance.SavePreseasonGames(_games);
            Debug.Log($"[Preseason] {_games.Count} amistosos guardados.");
        }

        // 3. Generar calendario de liga regular si no está generado
        if (season.generated == 0)
        {
            var allTeams = DatabaseManager.Instance.GetAllTeams();
            int count = ScheduleGenerator.GenerateSchedule(season, allTeams);

            // Marcar temporada como generada
            season.generated = 1;
            season.phase = "regular";
            DatabaseManager.Instance.UpdateSeason(season);

            Debug.Log($"[Preseason] Calendario generado: {count} partidos.");
        }

        ScreenManager.Instance.GoTo(GameScreen.Dashboard);
    }
}