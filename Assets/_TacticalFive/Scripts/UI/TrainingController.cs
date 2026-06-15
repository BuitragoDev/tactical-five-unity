using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class TrainingController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerSeason;
    private Label _headerDate;
    private Button _btnAction;

    // Left: player list
    private VisualElement _trainingBody;

    // Right
    private VisualElement _trainingRightBody;
    private VisualElement _asistenteCard;
    private VisualElement _noAsistente;
    private VisualElement _attrPanel;
    private VisualElement _attrBody;
    private Label _trainingEmpty;
    private VisualElement _trainingPlayerInfo;
    private Label _trainingPlayerName;
    private Label _trainingPlayerMeta;

    // Modal + auto
    private VisualElement _noAsistenteOverlay;
    private Button _btnNoAsistenteOk;
    private Button _btnAutoTrain;

    // Data
    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<PlayerData> _players;
    private List<TrainingData> _activeTraining;
    private EmployeeData _asistente;
    private PlayerData _selectedPlayer;
    private int _currentGameDay;

    private Dictionary<string, Sprite> _logoSprites = new();
    private Texture2D _starTex;
    private StyleBackground _starBg;
    private StyleBackground _empleadoBg;

    private static readonly Dictionary<string, string> PosLabels = new()
    {
        { "PG", "BASE" },
        { "SG", "ESCOLTA" },
        { "SF", "ALERO" },
        { "PF", "ALA-PIVOT" },
        { "C",  "PIVOT" }
    };

    private static readonly List<string> PosOrder =
        new() { "PG", "SG", "SF", "PF", "C" };

    private static readonly Dictionary<string, string> AttrDisplay = new()
    {
        { "shooting",    "TIRO" },
        { "three_point", "TRIPLE" },
        { "passing",     "PASE" },
        { "dribbling",   "BOTE" },
        { "defense",     "DEFENSA" },
        { "rebounding",  "REBOTE" },
        { "speed",       "VELOCIDAD" },
        { "athleticism", "ATLETISMO" },
        { "steals",      "ROBOS" },
        { "blocks",      "TAPONES" },
    };

    private static readonly Dictionary<string, string> BadgeAbbr = new()
    {
        { "shooting",    "T2" },
        { "three_point", "3P" },
        { "passing",     "PAS" },
        { "dribbling",   "BOT" },
        { "defense",     "DEF" },
        { "rebounding",  "REB" },
        { "speed",       "VEL" },
        { "athleticism", "ATL" },
        { "steals",      "ROB" },
        { "blocks",      "TAP" },
    };

    private static readonly Dictionary<string, Color32> AttrColors = new()
    {
        { "shooting",    new Color32(160, 45,  30,  255) },
        { "three_point", new Color32(25,  95,  155, 255) },
        { "passing",     new Color32(18,  110, 50,  255) },
        { "dribbling",   new Color32(95,  40,  125, 255) },
        { "defense",     new Color32(130, 28,  18,  255) },
        { "rebounding",  new Color32(150, 108, 6,   255) },
        { "speed",       new Color32(12,  120, 100, 255) },
        { "athleticism", new Color32(20,  135, 65,  255) },
        { "steals",      new Color32(160, 35,  20,  255) },
        { "blocks",      new Color32(0,   38,  77,  255) },
    };

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
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _btnAction = _root.Q<Button>("BtnAction");

        _trainingBody = _root.Q<VisualElement>("TrainingBody");
        _trainingRightBody = _root.Q<VisualElement>("TrainingRightBody");
        _asistenteCard = _root.Q<VisualElement>("AsistenteCard");
        _noAsistente = _root.Q<VisualElement>("NoAsistente");
        _attrPanel = _root.Q<VisualElement>("TrainingAttrPanel");
        _attrBody = _root.Q<VisualElement>("TrainingAttrBody");
        _trainingEmpty = _root.Q<Label>("TrainingEmpty");

        _noAsistenteOverlay = _root.Q<VisualElement>("NoAsistenteOverlay");
        _btnNoAsistenteOk = _root.Q<Button>("BtnNoAsistenteOk");
        _btnAutoTrain = _root.Q<Button>("BtnAutoTrain");

        _trainingPlayerInfo = _root.Q<VisualElement>("TrainingPlayerInfo");
        _trainingPlayerName = _root.Q<Label>("TrainingPlayerName");
        _trainingPlayerMeta = _root.Q<Label>("TrainingPlayerMeta");
    }

    void LoadSidebarIcons()
    {
        var iconMap = new Dictionary<string, string>
        {
            {"NavDashboardIcon", "inicio"},
            {"NavRosterIcon", "plantilla"},
            {"NavCalendarIcon", "calendario"},
            {"NavStandingsIcon", "clasificacion"},
            {"NavPalmaresIcon", "palmares"},
            {"NavResultsIcon", "resultados"},
            {"NavPlayoffsIcon", "playoff"},
            {"NavStatsIcon", "estadisticas"},
            {"NavMarketIcon", "mercado"},
            {"NavFinancesIcon", "finanzas"},
            {"NavArenaIcon", "pabellon"},
            {"NavMessagesIcon", "mensajes"},
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

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _starTex = Resources.Load<Texture2D>("Icons/star_24px");
        if (_starTex != null)
            _starBg = new StyleBackground(_starTex);
        _empleadoBg = new StyleBackground(Resources.Load<Texture2D>("Icons/empleado"));

        _manager = DatabaseManager.Instance.GetActiveManager();
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _activeTraining = DatabaseManager.Instance.GetTeamTraining(_myTeam.id);
        _currentGameDay = _season?.current_game_day ?? 0;

        var staff = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        _asistente = staff.FirstOrDefault(e => e.position == "ASISTENTE");
    }

    void RegisterCallbacks()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("RosterSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });
        _root.Q<Button>("SubmenuJugadores")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        _root.Q<Button>("SubmenuEmpleados")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Employees);
        });
        _root.Q<Button>("SubmenuLesionados")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Injured);
        });
        _root.Q<Button>("SubmenuQuinteto")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Quinteto); });
_root.Q<Button>("SubmenuVestuario")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Vestuario); });
        _root.Q<Button>("SubmenuEntrenamiento")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Training); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("PalmaresSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });
        _root.Q<Button>("SubmenuPalmares")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Palmares); });
        _root.Q<Button>("SubmenuRecords")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Records); });
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });

        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("MarketSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });

        _root.Q<Button>("SubmenuOfertas")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Market);
        });
        _root.Q<Button>("SubmenuCartera")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Cartera); });
        _root.Q<Button>("SubmenuHistorial")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Historial); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("FinanceSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });
        _root.Q<Button>("SubmenuDecisiones")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Finances);
        });
        _root.Q<Button>("SubmenuPrestamos")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Loans);
        });
        _root.Q<Button>("SubmenuSponsors")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("SubmenuTV")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });

        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

        _btnNoAsistenteOk?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _noAsistenteOverlay.RemoveFromClassList("training-modal-overlay--visible");
        });

        _btnAutoTrain?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnAutoAssign(); });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnAction);
            CursorManager.Instance.RegisterHandCursor(_btnAutoTrain);
        }
    }

    void Refresh()
    {
        RefreshHeader();
        BuildPlayerList();
        BuildAsistenteCard();
        _selectedPlayer = null;
        ShowEmptyState();
        _root.Q<Button>("SubmenuEntrenamiento")?.AddToClassList("nav-submenu-item--active");
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerBudget.text = $"${_myTeam.budget / 1_000_000}M";
        _headerBudget.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        var teamEmployees = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        long totalPayroll = _players.Sum(p => p.salary) + teamEmployees.Sum(e => e.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? 155_000_000;
        long margin = salaryCap - _players.Sum(p => p.salary);

        string marginText = margin >= 0
            ? $"+${margin / 1_000_000}M"
            : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        _headerMargin.text = marginText;
        _headerMargin.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) _headerMargin.AddToClassList("header-stat-value--negative");

        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        var chemLabel = _root.Q<Label>("HeaderChemistry");
        if (chemLabel != null)
        {
            chemLabel.text = chemistry.ToString();
            chemLabel.RemoveFromClassList("header-stat-value--gold");
            chemLabel.RemoveFromClassList("header-stat-value--negative");
            if (chemistry < 40)
                chemLabel.AddToClassList("header-stat-value--negative");
            else if (chemistry < 70)
                chemLabel.AddToClassList("header-stat-value--gold");
        }

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "DASHBOARD";
    }

    void BuildPlayerList()
    {
        _trainingBody.Clear();

        foreach (var pos in PosOrder)
        {
            var posPlayers = _players
                .Where(p => p.position == pos)
                .OrderByDescending(p => p.overall)
                .ToList();

            if (posPlayers.Count == 0) continue;

            var posHeader = new VisualElement();
            posHeader.AddToClassList("pos-header");

            var badge = new Label();
            badge.AddToClassList("pos-badge");
            badge.text = pos;

            var label = new Label();
            label.AddToClassList("pos-label");
            label.text = PosLabels.TryGetValue(pos, out var lbl) ? lbl : pos;

            posHeader.Add(badge);
            posHeader.Add(label);

            var section = new VisualElement();
            section.AddToClassList("pos-section");
            section.Add(posHeader);

            foreach (var player in posPlayers)
            {
                var row = CreateTrainingRow(player);
                section.Add(row);
            }

            _trainingBody.Add(section);
        }
    }

    VisualElement CreateTrainingRow(PlayerData player)
    {
        var row = new VisualElement();
        row.AddToClassList("training-row");

        var nameLbl = new Label();
        nameLbl.AddToClassList("training-row-name");
        nameLbl.text = player.is_rookie == 1
            ? $"{player.first_name} {player.last_name} (R)"
            : $"{player.first_name} {player.last_name}";

        var ovrLbl = new Label();
        ovrLbl.AddToClassList("training-row-ovr");
        ovrLbl.text = player.overall.ToString();

        row.Add(nameLbl);

        var active = _activeTraining.FirstOrDefault(t => t.player_id == player.id);
        if (active != null)
        {
            var badgeContainer = new VisualElement();
            badgeContainer.AddToClassList("training-badge");

            if (AttrColors.TryGetValue(active.attribute, out var badgeColor))
                badgeContainer.style.backgroundColor = new StyleColor(badgeColor);

            var badgeLbl = new Label();
            badgeLbl.AddToClassList("training-badge-label");
            badgeLbl.text = BadgeAbbr.TryGetValue(active.attribute, out var abbr) ? abbr : "TR";
            badgeContainer.Add(badgeLbl);
            row.Add(badgeContainer);
        }

        row.Add(ovrLbl);

        var ovrTextLbl = new Label();
        ovrTextLbl.AddToClassList("training-row-ovr-text");
        ovrTextLbl.text = "OVR";
        row.Add(ovrTextLbl);

        row.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnPlayerSelected(player, row); });
        if (CursorManager.Instance != null)
        {
            row.RegisterCallback<MouseEnterEvent>(_ =>
                CursorManager.Instance.SetHandCursor());
            row.RegisterCallback<MouseLeaveEvent>(_ =>
                CursorManager.Instance.SetDefaultCursor());
        }

        return row;
    }

    void BuildAsistenteCard()
    {
        _asistenteCard.Clear();
        _noAsistente.Clear();
        _noAsistente.style.display = DisplayStyle.None;
        _asistenteCard.style.display = DisplayStyle.Flex;

        if (_asistente != null)
        {
            var card = new VisualElement();
            card.AddToClassList("training-staff-card");

            var icon = new VisualElement();
            icon.AddToClassList("training-staff-icon");
            icon.style.backgroundImage = _empleadoBg;
            card.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("training-staff-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("training-staff-name");
            nameLbl.text = $"{_asistente.first_name} {_asistente.last_name}".ToUpper();
            info.Add(nameLbl);

            var starRow = new VisualElement();
            starRow.AddToClassList("training-staff-stars");
            for (int i = 0; i < 5; i++)
            {
                var star = new VisualElement();
                star.AddToClassList("training-staff-star");
                if (i >= _asistente.reputation)
                    star.AddToClassList("training-staff-star--empty");
                if (_starTex != null)
                    star.style.backgroundImage = _starBg;
                starRow.Add(star);
            }
            info.Add(starRow);

            int durationDays = GetTrainingDuration(_asistente.reputation);
            var interestLbl = new Label();
            interestLbl.AddToClassList("training-staff-interest");
            interestLbl.text = $"Duración del entrenamiento: {durationDays} días";
            info.Add(interestLbl);

            card.Add(info);
            _asistenteCard.Add(card);
        }
        else
        {
            _asistenteCard.style.display = DisplayStyle.None;
            _noAsistente.style.display = DisplayStyle.Flex;

            var lbl = new Label();
            lbl.AddToClassList("training-no-staff-label");
            lbl.text = "No hay Asistente contratado.\nVe a Empleados para contratar uno.";
            _noAsistente.Add(lbl);

            var hireBtn = new Button();
            hireBtn.AddToClassList("training-btn-hire");
            hireBtn.text = "IR A EMPLEADOS";
            hireBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                ScreenManager.Instance.GoTo(GameScreen.Employees);
            });
            if (CursorManager.Instance != null)
            {
                hireBtn.RegisterCallback<MouseEnterEvent>(_ =>
                    CursorManager.Instance.SetHandCursor());
                hireBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                    CursorManager.Instance.SetDefaultCursor());
            }
            _noAsistente.Add(hireBtn);
        }
    }

    void OnPlayerSelected(PlayerData player, VisualElement row)
    {
        _selectedPlayer = player;

        _trainingBody.Query<VisualElement>(className: "training-row--selected")
            .ForEach(e => e.RemoveFromClassList("training-row--selected"));
        row.AddToClassList("training-row--selected");

        if (_asistente != null)
            ShowAttrTable(player);
        else
            ShowEmptyState();

        ShowRightPanel();
    }

    void ShowAttrTable(PlayerData player)
    {
        _trainingEmpty.style.display = DisplayStyle.None;
        _attrPanel.style.display = DisplayStyle.Flex;
        _attrBody.Clear();

        _trainingPlayerInfo.AddToClassList("training-player-info--visible");
        _trainingPlayerName.text = $"{player.first_name} {player.last_name}".ToUpper();
        _trainingPlayerMeta.text = $"{player.age} años · {player.nationality} · {player.height_cm / 100f:F2}m · {player.weight_kg}kg{(player.is_rookie == 1 ? " · Rookie" : "")}";

        var activeTraining = _activeTraining.FirstOrDefault(t => t.player_id == player.id);

        var attrs = new (string field, string label)[]
        {
            ("shooting",    "TIRO"),
            ("three_point", "TRIPLE"),
            ("passing",     "PASE"),
            ("dribbling",   "BOTE"),
            ("defense",     "DEFENSA"),
            ("rebounding",  "REBOTE"),
            ("speed",       "VELOCIDAD"),
            ("athleticism", "ATLETISMO"),
            ("steals",      "ROBOS"),
            ("blocks",      "TAPONES"),
        };

        foreach (var (field, label) in attrs)
        {
            var row = new VisualElement();
            row.AddToClassList("training-attr-row");

            var val = (int)typeof(PlayerData).GetProperty(field).GetValue(player);
            bool atMax = val >= 99;
            bool isActiveAttr = activeTraining != null && activeTraining.attribute == field;
            bool disabled = atMax;

            var btn = new Button();
            btn.AddToClassList("training-attr-btn");
            if (AttrColors.TryGetValue(field, out var btnColor) && !isActiveAttr && !disabled)
                btn.style.backgroundColor = new StyleColor(btnColor);
            if (isActiveAttr || disabled)
                btn.AddToClassList("training-attr-btn--disabled");

            if (isActiveAttr)
                btn.text = "ENTRENANDO";
            else
                btn.text = "ENTRENAR";

            if (!isActiveAttr && !disabled)
            {
                btn.userData = field;
                btn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnAssignTraining(player, field); });
                if (CursorManager.Instance != null)
                {
                    btn.RegisterCallback<MouseEnterEvent>(_ =>
                        CursorManager.Instance.SetHandCursor());
                    btn.RegisterCallback<MouseLeaveEvent>(_ =>
                        CursorManager.Instance.SetDefaultCursor());
                }
            }

            var nameLbl = new Label();
            nameLbl.AddToClassList("training-attr-name");
            nameLbl.text = label;

            var valLbl = new Label();
            valLbl.AddToClassList("training-attr-value");
            valLbl.text = val.ToString();

            row.Add(btn);
            row.Add(nameLbl);
            row.Add(valLbl);
            _attrBody.Add(row);
        }
    }

    void ShowEmptyState()
    {
        _trainingEmpty.style.display = DisplayStyle.Flex;
        _attrPanel.style.display = DisplayStyle.None;
        _trainingPlayerInfo.RemoveFromClassList("training-player-info--visible");
    }

    void ShowRightPanel()
    {
        _trainingEmpty.style.display = DisplayStyle.None;
    }

    void OnAssignTraining(PlayerData player, string attribute)
    {
        if (_asistente == null)
        {
            _noAsistenteOverlay.AddToClassList("training-modal-overlay--visible");
            return;
        }

        var existing = _activeTraining.FirstOrDefault(t => t.player_id == player.id);
        int duration = GetTrainingDuration(_asistente.reputation);

        if (existing != null)
        {
            existing.attribute = attribute;
            existing.start_day = _currentGameDay;
            existing.duration = duration;
            DatabaseManager.Instance.Db.Update(existing);
            Debug.Log($"[Training] Entrenamiento cambiado: {player.first_name} {player.last_name} → {attribute} ({duration} días)");
        }
        else
        {
            var training = new TrainingData
            {
                player_id = player.id,
                team_id = _myTeam.id,
                attribute = attribute,
                start_day = _currentGameDay,
                duration = duration,
                completed = 0,
            };
            DatabaseManager.Instance.InsertTraining(training);
            Debug.Log($"[Training] Entrenamiento asignado: {player.first_name} {player.last_name} → {attribute} ({duration} días)");
        }

        _activeTraining = DatabaseManager.Instance.GetTeamTraining(_myTeam.id);
        BuildPlayerList();
        OnPlayerSelected(player, _trainingBody.Query<VisualElement>(className: "training-row--selected").First());
    }

    private static readonly Dictionary<string, string[]> PrimaryAttrs = new()
    {
        { "PG", new[] { "passing", "dribbling", "three_point", "speed", "shooting" } },
        { "SG", new[] { "shooting", "three_point", "speed", "dribbling" } },
        { "SF", new[] { "shooting", "three_point", "defense", "speed", "athleticism" } },
        { "PF", new[] { "defense", "rebounding", "shooting", "blocks", "athleticism" } },
        { "C",  new[] { "rebounding", "blocks", "defense", "shooting", "athleticism" } },
    };

    private static readonly Dictionary<string, string[]> SecondaryAttrs = new()
    {
        { "PG", new[] { "defense", "steals", "athleticism" } },
        { "SG", new[] { "passing", "defense", "steals", "athleticism" } },
        { "SF", new[] { "passing", "dribbling", "rebounding", "steals" } },
        { "PF", new[] { "three_point", "passing", "speed" } },
        { "C",  new[] { "passing", "speed" } },
    };

    private static readonly Dictionary<string, string[]> ExcludedAttrs = new()
    {
        { "PG", new[] { "rebounding", "blocks" } },
        { "SG", new[] { "rebounding", "blocks" } },
        { "SF", new[] { "blocks" } },
        { "PF", new[] { "dribbling", "steals" } },
        { "C",  new[] { "three_point", "dribbling", "steals" } },
    };

    void OnAutoAssign()
    {
        if (_asistente == null)
        {
            _noAsistenteOverlay.AddToClassList("training-modal-overlay--visible");
            return;
        }

        int duration = GetTrainingDuration(_asistente.reputation);
        int count = 0;

        foreach (var player in _players)
        {
            string pos = player.position;
            var allTrainable = new[] { "shooting", "three_point", "passing", "dribbling", "defense",
                                       "rebounding", "speed", "athleticism", "steals", "blocks" };

            // Try primary attributes first
            string attr = PickLowest(player, pos, PrimaryAttrs, null, 0);
            // If all primaries ≥ 80, try secondary
            if (attr == null)
                attr = PickLowest(player, pos, SecondaryAttrs, null, 80);
            // If secondaries also ≥ 80, try everything except excluded
            if (attr == null)
                attr = PickLowest(player, null, null, ExcludedAttrs.TryGetValue(pos, out var exc) ? exc : null, 99);

            if (attr == null) continue;

            var existing = _activeTraining.FirstOrDefault(t => t.player_id == player.id);
            if (existing != null)
            {
                existing.attribute = attr;
                existing.start_day = _currentGameDay;
                existing.duration = duration;
                DatabaseManager.Instance.Db.Update(existing);
            }
            else
            {
                var training = new TrainingData
                {
                    player_id = player.id,
                    team_id = _myTeam.id,
                    attribute = attr,
                    start_day = _currentGameDay,
                    duration = duration,
                    completed = 0,
                };
                DatabaseManager.Instance.InsertTraining(training);
            }
            count++;
        }

        Debug.Log($"[Training] Auto-asignados {count} entrenamientos");

        _activeTraining = DatabaseManager.Instance.GetTeamTraining(_myTeam.id);
        BuildPlayerList();

        if (_selectedPlayer != null)
        {
            var row = _trainingBody.Query<VisualElement>(className: "training-row--selected").First();
            if (row != null)
                OnPlayerSelected(_selectedPlayer, row);
        }
    }

    string PickLowest(PlayerData player, string pos, Dictionary<string, string[]> groupMap, string[] excluded, int fallbackThreshold)
    {
        string[] pool;
        if (groupMap != null && pos != null && groupMap.TryGetValue(pos, out var p))
            pool = p;
        else if (excluded != null)
            pool = new[] { "shooting", "three_point", "passing", "dribbling", "defense",
                           "rebounding", "speed", "athleticism", "steals", "blocks" }
                   .Where(a => !excluded.Contains(a)).ToArray();
        else
            return null;

        string weakest = null;
        int minVal = 100;
        bool allAboveThreshold = true;
        foreach (var attr in pool)
        {
            int val = (int)typeof(PlayerData).GetProperty(attr).GetValue(player);
            if (val < fallbackThreshold)
                allAboveThreshold = false;
            if (val < minVal)
            {
                minVal = val;
                weakest = attr;
            }
        }
        // If all attributes in this pool are ≥ fallbackThreshold, return null to try next pool
        if (allAboveThreshold)
            return null;
        return weakest;
    }

    int GetTrainingDuration(int reputation)
    {
        return reputation switch
        {
            5 => 14,
            4 => 18,
            3 => 22,
            2 => 26,
            _ => 30,
        };
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
