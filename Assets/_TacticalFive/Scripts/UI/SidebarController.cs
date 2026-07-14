using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public static class SidebarController
{
    struct ScreenNavMapping
    {
        public GameScreen screen;
        public string navName;
        public string submenuName;
        public string submenuItemName;
    }

    static readonly ScreenNavMapping[] Mappings = new ScreenNavMapping[]
    {
        new() { screen = GameScreen.Dashboard,   navName = "NavDashboard" },
        new() { screen = GameScreen.Roster,      navName = "NavRoster",    submenuName = "RosterSubmenu",  submenuItemName = "SubmenuJugadores" },
        new() { screen = GameScreen.Quinteto,    navName = "NavRoster",    submenuName = "RosterSubmenu",  submenuItemName = "SubmenuQuinteto" },
        new() { screen = GameScreen.Training,    navName = "NavRoster",    submenuName = "RosterSubmenu",  submenuItemName = "SubmenuEntrenamiento" },
        new() { screen = GameScreen.Employees,   navName = "NavRoster",    submenuName = "RosterSubmenu",  submenuItemName = "SubmenuEmpleados" },
        new() { screen = GameScreen.Injured,     navName = "NavRoster",    submenuName = "RosterSubmenu",  submenuItemName = "SubmenuLesionados" },

        new() { screen = GameScreen.Calendar,    navName = "NavCalendar" },
        new() { screen = GameScreen.Results,     navName = "NavResults" },
        new() { screen = GameScreen.Standings,   navName = "NavStandings" },
        new() { screen = GameScreen.Palmares,    navName = "NavPalmares",  submenuName = "PalmaresSubmenu", submenuItemName = "SubmenuPalmares" },
        new() { screen = GameScreen.Records,     navName = "NavPalmares",  submenuName = "PalmaresSubmenu", submenuItemName = "SubmenuRecords" },
        new() { screen = GameScreen.Playoffs,    navName = "NavPlayoffs" },
        new() { screen = GameScreen.Stats,       navName = "NavStats" },
        new() { screen = GameScreen.Market,      navName = "NavMarket",    submenuName = "MarketSubmenu",   submenuItemName = "SubmenuOfertas" },
        new() { screen = GameScreen.Cartera,     navName = "NavMarket",    submenuName = "MarketSubmenu",   submenuItemName = "SubmenuCartera" },
        new() { screen = GameScreen.Historial,   navName = "NavMarket",    submenuName = "MarketSubmenu",   submenuItemName = "SubmenuHistorial" },
        new() { screen = GameScreen.Finances,    navName = "NavFinances",  submenuName = "FinanceSubmenu",  submenuItemName = "SubmenuDecisiones" },
        new() { screen = GameScreen.Loans,       navName = "NavFinances",  submenuName = "FinanceSubmenu",  submenuItemName = "SubmenuPrestamos" },
        new() { screen = GameScreen.Sponsors,    navName = "NavFinances",  submenuName = "FinanceSubmenu",  submenuItemName = "SubmenuSponsors" },
        new() { screen = GameScreen.TV,          navName = "NavFinances",  submenuName = "FinanceSubmenu",  submenuItemName = "SubmenuTV" },
        new() { screen = GameScreen.Arena,       navName = "NavArena" },
        new() { screen = GameScreen.Manager,     navName = "NavManager" },
        new() { screen = GameScreen.Messages,    navName = "NavMessages" },
    };

    static VisualTreeAsset _sidebarTemplate;

    static VisualTreeAsset GetTemplate()
    {
        if (_sidebarTemplate == null)
            _sidebarTemplate = Resources.Load<VisualTreeAsset>("UI/Core/Sidebar");
        return _sidebarTemplate;
    }

    public static void Attach(VisualElement root, GameScreen activeScreen)
    {
        var template = GetTemplate();
        if (template == null)
        {
            Debug.LogError("[Sidebar] Cannot load Sidebar.uxml from Resources/UI/Core/Sidebar");
            return;
        }

        // Each screen's container (DashboardRoot, RosterRoot, etc.) is the first child of root
        var container = root.childCount > 0 ? root[0] : root;
        if (container == null) container = root;

        var sidebar = template.CloneTree();
        container.Insert(0, sidebar);

        var mapping = FindMapping(activeScreen);
        if (mapping != null)
        {
            var activeNav = sidebar.Q<Button>(mapping.Value.navName);
            if (activeNav != null) activeNav.AddToClassList("nav-item--active");

            if (!string.IsNullOrEmpty(mapping.Value.submenuName))
            {
                var activeSub = sidebar.Q<VisualElement>(mapping.Value.submenuName);
                if (activeSub != null) activeSub.AddToClassList("nav-submenu--visible");
            }

            if (!string.IsNullOrEmpty(mapping.Value.submenuItemName))
            {
                var activeItem = sidebar.Q<Button>(mapping.Value.submenuItemName);
                if (activeItem != null) activeItem.AddToClassList("nav-submenu-item--active");
            }
        }

        LoadIcons(sidebar);

        if (CursorManager.Instance != null)
        {
            var cursorNames = new[]
            {
                "NavDashboard", "NavRoster", "NavCalendar", "NavStandings",
                "NavPalmares", "NavResults", "NavPlayoffs", "NavStats",
                "NavMarket", "NavFinances", "NavArena", "NavManager", "NavMessages",
                "SubmenuJugadores", "SubmenuQuinteto", "SubmenuEntrenamiento",
                "SubmenuEmpleados", "SubmenuLesionados",
                "SubmenuPalmares", "SubmenuRecords",
                "SubmenuOfertas", "SubmenuCartera", "SubmenuHistorial",
                "SubmenuDecisiones", "SubmenuPrestamos", "SubmenuSponsors", "SubmenuTV",
            };
            foreach (var name in cursorNames)
            {
                var el = sidebar.Q<VisualElement>(name);
                if (el != null)
                    CursorManager.Instance.RegisterHandCursor(el);
            }
        }
    }

    static ScreenNavMapping? FindMapping(GameScreen screen)
    {
        for (int i = 0; i < Mappings.Length; i++)
        {
            if (Mappings[i].screen == screen)
                return Mappings[i];
        }
        return null;
    }

    static void LoadIcons(VisualElement sidebar)
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
            {"NavManagerIcon", "manager"},
            {"NavMessagesIcon", "mensajes"},
        };

        foreach (var kv in iconMap)
        {
            var iconElem = sidebar.Q<VisualElement>(kv.Key);
            if (iconElem == null) continue;
            var tex = Resources.Load<Texture2D>($"Icons/{kv.Value}");
            if (tex != null)
                iconElem.style.backgroundImage = new StyleBackground(tex);
        }
    }

    public static void PlayClick()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("click");
    }
}
