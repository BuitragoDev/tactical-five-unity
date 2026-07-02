using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class VestuarioController : MonoBehaviour
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
    private Label _headerChemistry;
    private Label _headerSeason;
    private Label _headerDate;
    private Button _btnAction;

    // Overview
    private VisualElement _chemRing;
    private Label _chemRingVal;
    private Label _chemDesc;

    // Personalities
    private VisualElement _personalitiesBody;

    // Relations
    private VisualElement _relationsBody;
    private Label _relationsEmpty;

    // Data
    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<PlayerData> _players;
    private List<PlayerPersonalityData> _personalities;
    private List<PlayerRelationshipData> _relationships;

    private Dictionary<string, Sprite> _logoSprites = new();

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
        CursorManager.Instance?.SetDefaultCursor();
    }

    void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerChemistry = _root.Q<Label>("HeaderChemistry");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _btnAction = _root.Q<Button>("BtnAction");

        _chemRing = _root.Q<VisualElement>("ChemRing");
        _chemRingVal = _root.Q<Label>("ChemRingVal");
        _chemDesc = _root.Q<Label>("ChemDesc");

        _personalitiesBody = _root.Q<VisualElement>("PersonalitiesBody");
        _relationsBody = _root.Q<VisualElement>("RelationsBody");
        _relationsEmpty = _root.Q<Label>("RelationsEmpty");
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

        _manager = DatabaseManager.Instance.GetActiveManager();
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);

        _personalities = DatabaseManager.Instance.GetTeamPersonalities(_myTeam.id);
        _relationships = DatabaseManager.Instance.GetTeamRelationships(_myTeam.id);
    }

    void RegisterCallbacks()
    {
        // Sidebar unificado
        SidebarController.Attach(_root, GameScreen.Vestuario);
        HeaderController.Attach(_root);
        var allSubmenus = new[] {
            _root.Q<VisualElement>("RosterSubmenu"),
            _root.Q<VisualElement>("PalmaresSubmenu"),
            _root.Q<VisualElement>("MarketSubmenu"),
            _root.Q<VisualElement>("FinanceSubmenu")
        };

        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("RosterSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
        });
        _root.Q<Button>("SubmenuJugadores")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        _root.Q<Button>("SubmenuEntrenamiento")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Training);
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
_root.Q<Button>("SubmenuVestuario")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Vestuario);
        });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("PalmaresSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
        });
        _root.Q<Button>("SubmenuPalmares")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Palmares); });
        _root.Q<Button>("SubmenuRecords")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Records); });
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
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
        });
        _root.Q<Button>("SubmenuOfertas")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Market);
        });
        _root.Q<Button>("SubmenuCartera")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Cartera); });
        _root.Q<Button>("SubmenuHistorial")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Historial); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("FinanceSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
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
        _root.Q<Button>("SubmenuSponsors")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("SubmenuTV")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });

        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

        if (CursorManager.Instance == null) return;
        var cursor = CursorManager.Instance;
        cursor.RegisterHandCursor(_root.Q<Button>("NavDashboard"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavRoster"));
        foreach (var btn in _root.Query<Button>(null, "nav-submenu-item").Build())
            cursor.RegisterHandCursor(btn);
        cursor.RegisterHandCursor(_root.Q<Button>("NavCalendar"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavStandings"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavPalmares"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavResults"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavPlayoffs"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavStats"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavMarket"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavFinances"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavArena"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavMessages"));
        cursor.RegisterHandCursor(_root.Q<VisualElement>("ConfigIcon"));
        cursor.RegisterHandCursor(_btnAction);
    }

    void Refresh()
    {
        RefreshHeader();
        DatabaseManager.Instance.EnsureTeamRelationshipsSeeded(_myTeam.id);
        _relationships = DatabaseManager.Instance.GetTeamRelationships(_myTeam.id);
        _personalities = DatabaseManager.Instance.GetTeamPersonalities(_myTeam.id);
        BuildChemistryOverview();
        BuildPersonalitiesLegend();
        BuildPersonalitiesList();
        BuildRelationsList();
        _root.Q<VisualElement>("RosterSubmenu")?.AddToClassList("nav-submenu--visible");
        _root.Q<Button>("SubmenuVestuario")?.AddToClassList("nav-submenu-item--active");
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;
        if (_headerTeamName == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerBudget.text = $"${_myTeam.budget / 1_000_000}M";
        _headerBudget.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        long totalPayroll = _players.Sum(p => p.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - _players.Sum(p => p.salary);

        string marginText = margin >= 0
            ? $"+${margin / 1_000_000}M"
            : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        _headerMargin.text = marginText;
        _headerChemistry.text = $"{chemistry}%";
        _headerChemistry.RemoveFromClassList("header-stat-value--gold");
        _headerChemistry.RemoveFromClassList("header-stat-value--negative");
        if (chemistry < 40)
            _headerChemistry.AddToClassList("header-stat-value--negative");
        else if (chemistry < 70)
            _headerChemistry.AddToClassList("header-stat-value--gold");

        _headerMargin.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) _headerMargin.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "DASHBOARD";
    }

    void BuildChemistryOverview()
    {
        int teamChemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        _chemRingVal.text = $"{teamChemistry}%";
        SetChemRingColor(teamChemistry);

        if (_relationships.Count == 0)
        {
            _chemDesc.text = "No hay suficientes datos de relaciones. Juega m\u00e1s partidos para generar v\u00ednculos entre jugadores.";
        }
        else
        {
            if (teamChemistry >= 70)
                _chemDesc.text = "El vestuario tiene una qu\u00edmica excelente. Los jugadores se compenetran bien dentro y fuera de la pista.";
            else if (teamChemistry >= 40)
                _chemDesc.text = "La qu\u00edmica del vestuario es aceptable. Con trabajo se puede mejorar la cohesi\u00f3n del grupo.";
            else
                _chemDesc.text = "La qu\u00edmica del vestuario es baja. Hay tensiones entre jugadores que afectan al rendimiento del equipo.";
        }

        _headerChemistry.text = $"{teamChemistry}%";
        _headerChemistry.style.color = teamChemistry >= 70 ? new StyleColor(new Color(39f / 255, 174f / 255, 96f / 255)) :
                                         teamChemistry >= 40 ? new StyleColor(new Color(212f / 255, 160f / 255, 23f / 255)) :
                                         new StyleColor(new Color(192f / 255, 57f / 255, 43f / 255));
    }

    void SetChemRingColor(int bond)
    {
        Color c;
        if (bond >= 70)
            c = new Color(39f / 255, 174f / 255, 96f / 255);
        else if (bond >= 40)
            c = new Color(212f / 255, 160f / 255, 23f / 255);
        else
            c = new Color(192f / 255, 57f / 255, 43f / 255);
        _chemRing.style.borderTopColor = c;
        _chemRing.style.borderLeftColor = c;
        _chemRing.style.borderRightColor = c;
        _chemRing.style.borderBottomColor = c;

        _chemRingVal.style.color = new StyleColor(c);
    }

    void BuildPersonalitiesLegend()
    {
        var legend = _root.Q<VisualElement>("PersonalitiesLegend");
        if (legend == null) return;
        legend.Clear();

        var types = new (string name, string colorClass)[]
        {
            ("Líder", "pers-color--lider"),
            ("Mentor", "pers-color--mentor"),
            ("Estrella", "pers-color--estrella"),
            ("Guerrero", "pers-color--guerrero"),
            ("Tranquilo", "pers-color--tranquilo"),
            ("Intenso", "pers-color--intenso"),
            ("Profesional", "pers-color--profesional"),
            ("Novato", "pers-color--novato"),
        };

        foreach (var (name, colorClass) in types)
        {
            var item = new VisualElement();
            item.AddToClassList("pers-legend-item");

            var swatch = new VisualElement();
            swatch.AddToClassList("pers-legend-swatch");
            swatch.AddToClassList(colorClass);
            item.Add(swatch);

            var label = new Label();
            label.AddToClassList("pers-legend-label");
            label.text = name.ToUpper();
            item.Add(label);

            legend.Add(item);
        }
    }

    string PersonalityColorClass(string type)
    {
        return type switch
        {
            "Líder" => "pers-color--lider",
            "Mentor" => "pers-color--mentor",
            "Estrella" => "pers-color--estrella",
            "Guerrero" => "pers-color--guerrero",
            "Tranquilo" => "pers-color--tranquilo",
            "Intenso" => "pers-color--intenso",
            "Profesional" => "pers-color--profesional",
            "Novato" => "pers-color--novato",
            _ => ""
        };
    }

    void BuildPersonalitiesList()
    {
        _personalitiesBody.Clear();

        var sorted = _players.OrderBy(p => p.position).ThenBy(p => p.last_name).ToList();
        foreach (var player in sorted)
        {
            var card = new VisualElement();
            card.AddToClassList("personality-card");

            var pers = _personalities.FirstOrDefault(p => p.player_id == player.id);

            var avatar = new VisualElement();
            avatar.AddToClassList("personality-card-avatar");
            if (pers != null)
            {
                var colorClass = PersonalityColorClass(pers.personality_type);
                if (!string.IsNullOrEmpty(colorClass))
                    avatar.AddToClassList(colorClass);
            }
            card.Add(avatar);

            var info = new VisualElement();
            info.AddToClassList("personality-card-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("personality-card-name");
            nameLbl.text = $"{player.first_name} {player.last_name}";
            info.Add(nameLbl);

            var meta = new VisualElement();
            meta.AddToClassList("personality-card-meta");

            if (pers != null)
            {
                var typeLbl = new Label();
                typeLbl.AddToClassList("personality-card-type");
                typeLbl.text = pers.personality_type.ToUpper();
                meta.Add(typeLbl);

                var traitsLbl = new Label();
                traitsLbl.AddToClassList("personality-card-traits");
                traitsLbl.text = $"{pers.trait_1} \u2022 {pers.trait_2}";
                meta.Add(traitsLbl);
            }

            var posLbl = new Label();
            posLbl.AddToClassList("personality-card-pos");
            posLbl.text = player.position;
            meta.Add(posLbl);

            info.Add(meta);
            card.Add(info);
            _personalitiesBody.Add(card);
        }
    }

    void BuildRelationsList()
    {
        _relationsBody.Clear();

        if (_relationships.Count == 0)
        {
            _relationsEmpty = new Label();
            _relationsEmpty.text = "No hay relaciones registradas. Juega partidos para que los jugadores desarrollen v\u00ednculos.";
            _relationsEmpty.AddToClassList("relations-empty");
            _relationsBody.Add(_relationsEmpty);
            return;
        }

        var sorted = _relationships.OrderByDescending(r => r.bond).ToList();

        foreach (var rel in sorted)
        {
            var playerA = _players.FirstOrDefault(p => p.id == rel.player_a_id);
            var playerB = _players.FirstOrDefault(p => p.id == rel.player_b_id);
            if (playerA == null || playerB == null) continue;

            var row = new VisualElement();
            row.AddToClassList("relation-row");

            // Player A
            row.Add(BuildPlayerSide(playerA));

            // Bond area
            row.Add(BuildBondArea(rel.bond));

            // Player B
            row.Add(BuildPlayerSide(playerB));

            _relationsBody.Add(row);
        }
    }

    VisualElement BuildPlayerSide(PlayerData player)
    {
        var container = new VisualElement();
        container.AddToClassList("relation-player");

        var avatar = new VisualElement();
        avatar.AddToClassList("relation-player-avatar");
        var pers = _personalities.FirstOrDefault(p => p.player_id == player.id);
        if (pers != null)
        {
            var colorClass = PersonalityColorClass(pers.personality_type);
            if (!string.IsNullOrEmpty(colorClass))
                avatar.AddToClassList(colorClass);
        }
        Texture2D tex = PlayerPhotoHelper.Load(player.id, player.photo);
        if (tex != null)
            avatar.style.backgroundImage = new StyleBackground(tex);
        container.Add(avatar);

        var nameLbl = new Label();
        nameLbl.AddToClassList("relation-player-name");
        nameLbl.text = $"{player.first_name} {player.last_name}";
        container.Add(nameLbl);

        var posLbl = new Label();
        posLbl.AddToClassList("relation-player-pos");
        posLbl.text = player.position;
        container.Add(posLbl);

        return container;
    }

    VisualElement BuildBondArea(int bond)
    {
        var container = new VisualElement();
        container.AddToClassList("relation-bond-area");

        string colorClass;
        string label;
        if (bond >= 70) { colorClass = "bond--high"; label = "FUERTE"; }
        else if (bond >= 40) { colorClass = "bond--mid"; label = "MEDIA"; }
        else { colorClass = "bond--low"; label = "D\u00c9BIL"; }

        var valLbl = new Label();
        valLbl.AddToClassList("relation-bond-value");
        valLbl.AddToClassList(colorClass);
        valLbl.text = $"{bond}";
        container.Add(valLbl);

        var barBg = new VisualElement();
        barBg.AddToClassList("relation-bond-bar-bg");
        var barFill = new VisualElement();
        barFill.AddToClassList("relation-bond-bar-fill");
        string barClass = bond >= 70 ? "bond-bar--high" : bond >= 40 ? "bond-bar--mid" : "bond-bar--low";
        barFill.AddToClassList(barClass);
        barFill.style.width = new StyleLength(new Length(bond, LengthUnit.Percent));
        barBg.Add(barFill);
        container.Add(barBg);

        var labelLbl = new Label();
        labelLbl.AddToClassList("relation-bond-label");
        labelLbl.AddToClassList(colorClass);
        labelLbl.text = label;
        container.Add(labelLbl);

        return container;
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
