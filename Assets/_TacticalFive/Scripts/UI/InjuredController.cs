using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class InjuredController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerInjuredCount;
    private Label _headerSeason;
    private Label _headerDate;
    private Button _btnAction;

    // Medical staff
    private VisualElement _medStaffBody;

    // Injured table
    private VisualElement _injuredTable;

    // Treatment result modal
    private VisualElement _treatmentResultOverlay;
    private Label _treatmentResultTitle;
    private Label _treatmentResultText;
    private Button _btnTreatmentOk;

    // Data
    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<PlayerData> _injuredPlayers;
    private EmployeeData _medico;

    private Dictionary<string, Sprite> _logoSprites = new();
    private Texture2D _starTex;
    private StyleBackground _starBg;
    private StyleBackground _empleadoBg;

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
        _headerInjuredCount = _root.Q<Label>("HeaderInjuredCount");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _btnAction = _root.Q<Button>("BtnAction");

        _medStaffBody = _root.Q<VisualElement>("MedStaffBody");
        _injuredTable = _root.Q<VisualElement>("InjuredTable");

        _treatmentResultOverlay = _root.Q<VisualElement>("TreatmentResultOverlay");
        _treatmentResultTitle = _root.Q<Label>("TreatmentResultTitle");
        _treatmentResultText = _root.Q<Label>("TreatmentResultText");
        _btnTreatmentOk = _root.Q<Button>("BtnTreatmentOk");
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
        ReloadData();
    }

    void ReloadData()
    {
        var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _injuredPlayers = allPlayers.Where(p => p.injury_days > 0).ToList();

        var staff = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        _medico = staff.FirstOrDefault(e => e.position == "MEDICO");
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
        _btnTreatmentOk?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseTreatmentResult(); });

        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(_btnAction);
    }

    void Refresh()
    {
        RefreshHeader();
        BuildMedicalStaff();
        BuildInjuredTable();
        _root.Q<Button>("SubmenuLesionados")?.AddToClassList("nav-submenu-item--active");
        _treatmentResultOverlay.style.display = DisplayStyle.None;
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerInjuredCount.text = _injuredPlayers.Count.ToString();

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "DASHBOARD";
    }

    void BuildMedicalStaff()
    {
        _medStaffBody.Clear();

        if (_medico != null)
        {
            var card = new VisualElement();
            card.AddToClassList("med-staff-card");

            var icon = new VisualElement();
            icon.AddToClassList("med-staff-icon");
            icon.style.backgroundImage = _empleadoBg;
            card.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("med-staff-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("med-staff-name");
            nameLbl.text = $"{_medico.first_name} {_medico.last_name}".ToUpper();
            info.Add(nameLbl);

            var repRow = new VisualElement();
            repRow.style.flexDirection = FlexDirection.Row;
            repRow.style.marginTop = 4;
            for (int i = 0; i < 5; i++)
            {
                var star = new VisualElement();
                star.AddToClassList("med-staff-star");
                if (i >= _medico.reputation)
                    star.AddToClassList("med-staff-star--empty");
                if (_starTex != null)
                    star.style.backgroundImage = _starBg;
                repRow.Add(star);
            }
            info.Add(repRow);

            card.Add(info);
            _medStaffBody.Add(card);
        }
        else
        {
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("med-staff-empty");
            emptyLbl.text = "A\u00fan no se ha contratado ning\u00fan jefe de servicios m\u00e9dicos.";
            _medStaffBody.Add(emptyLbl);
        }
    }

    void BuildInjuredTable()
    {
        _injuredTable.Clear();

        // Header row
        var headerRow = new VisualElement();
        headerRow.AddToClassList("injured-header-row");

        var hNum = new Label(); hNum.AddToClassList("injured-col-num"); hNum.text = "#"; headerRow.Add(hNum);
        var hName = new Label(); hName.AddToClassList("injured-col-name"); hName.text = "JUGADOR"; headerRow.Add(hName);
        var hPos = new Label(); hPos.AddToClassList("injured-col-pos"); hPos.text = "POS"; headerRow.Add(hPos);
        var hInjury = new Label(); hInjury.AddToClassList("injured-col-injury"); hInjury.text = "LESI\u00d3N"; headerRow.Add(hInjury);
        var hDays = new Label(); hDays.AddToClassList("injured-col-days"); hDays.text = "D\u00cdAS"; headerRow.Add(hDays);
        var hAct = new Label(); hAct.AddToClassList("injured-col-action"); hAct.text = "TRATAR"; headerRow.Add(hAct);
        _injuredTable.Add(headerRow);

        if (_injuredPlayers.Count == 0)
        {
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("injured-empty");
            emptyLbl.text = "No hay jugadores lesionados.";
            _injuredTable.Add(emptyLbl);
            return;
        }

        bool hasMedico = _medico != null;

        for (int i = 0; i < _injuredPlayers.Count; i++)
        {
            var player = _injuredPlayers[i];
            var row = new VisualElement();
            row.AddToClassList("injured-row");

            var numLbl = new Label();
            numLbl.AddToClassList("injured-row-num");
            numLbl.text = (i + 1).ToString("D2");
            row.Add(numLbl);

            var nameLbl = new Label();
            nameLbl.AddToClassList("injured-row-name");
            nameLbl.text = $"{player.first_name} {player.last_name}".ToUpper();
            row.Add(nameLbl);

            var posLbl = new Label();
            posLbl.AddToClassList("injured-row-pos");
            posLbl.text = player.position;
            row.Add(posLbl);

            var injuryLbl = new Label();
            injuryLbl.AddToClassList("injured-row-injury");
            injuryLbl.text = string.IsNullOrEmpty(player.injury_type) ? "LESI\u00d3N" : player.injury_type;
            row.Add(injuryLbl);

            var daysLbl = new Label();
            daysLbl.AddToClassList("injured-row-days");
            daysLbl.text = $"{player.injury_days} d\u00eda{(player.injury_days != 1 ? "s" : "")}";
            row.Add(daysLbl);

            bool alreadyTreated = player.treated == 1;
            var treatBtn = new Button();
            treatBtn.AddToClassList("btn-treat");
            treatBtn.text = alreadyTreated ? "TRATADO" : "TRATAR";
            if (!hasMedico || alreadyTreated)
            {
                treatBtn.AddToClassList("btn-treat--disabled");
                treatBtn.SetEnabled(false);
            }
            else
            {
                treatBtn.userData = player;
                treatBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnTreat(player); });
                if (CursorManager.Instance != null)
                {
                    treatBtn.RegisterCallback<MouseEnterEvent>(_ =>
                        CursorManager.Instance.SetHandCursor());
                    treatBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                        CursorManager.Instance.SetDefaultCursor());
                }
            }
            row.Add(treatBtn);

            _injuredTable.Add(row);
        }
    }

    // ── TREATMENT ──

    void OnTreat(PlayerData player)
    {
        if (_medico == null) return;

        float pct = _medico.reputation switch
        {
            5 => Random.Range(0.25f, 0.40f),
            4 => Random.Range(0.20f, 0.32f),
            3 => Random.Range(0.15f, 0.25f),
            2 => Random.Range(0.10f, 0.18f),
            _ => Random.Range(0.05f, 0.12f),
        };

        int oldDays = player.injury_days;
        int newDays = Mathf.CeilToInt(player.injury_days * (1f - pct));
        player.injury_days = Mathf.Clamp(newDays, 1, player.injury_days);
        player.treated = 1;
        DatabaseManager.Instance.UpdatePlayer(player);

        string playerName = $"{player.first_name} {player.last_name}";
        string reductionText = $"{playerName} ha recibido tratamiento m\u00e9dico.\nSus d\u00edas de baja se reducen de {oldDays} a {player.injury_days}.";

        ReloadData();
        Refresh();

        _treatmentResultTitle.text = "TRATAMIENTO COMPLETADO";
        _treatmentResultText.text = reductionText;
        _treatmentResultOverlay.style.display = DisplayStyle.Flex;
    }

    void CloseTreatmentResult()
    {
        _treatmentResultOverlay.style.display = DisplayStyle.None;
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
