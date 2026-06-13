using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class HistorialController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _historialBody;
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Button _btnAction;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TradeData> _trades = new();
    private Dictionary<int, TeamData> _teamCache = new();
    private Dictionary<int, PlayerData> _playerCache = new();

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
        _historialBody = _root.Q<VisualElement>("HistorialBody");

        var scrollView = _root.Q<ScrollView>();
        if (scrollView != null)
            scrollView.contentContainer.style.flexGrow = 0;
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _btnAction = _root.Q<Button>("BtnAction");

        _root.Q<Label>("HeaderSeason").text = "";
        _root.Q<Label>("HeaderDate").text = "";
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
            {"NavRecordsIcon", "records"},
            {"NavMarketIcon", "mercado"},
            {"NavFinancesIcon", "finanzas"},
            {"NavSponsorsIcon", "patrocinador"},
            {"NavTVIcon", "television"},
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
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        if (_season == null) return;

        _trades = DatabaseManager.Instance.GetTradesBySeason(_season.id);

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        foreach (var t in allTeams)
            _teamCache[t.id] = t;

        foreach (var tr in _trades)
        {
            if (!_playerCache.ContainsKey(tr.player_id))
            {
                var p = DatabaseManager.Instance.GetPlayerById(tr.player_id);
                if (p != null)
                    _playerCache[tr.player_id] = p;
            }
        }
    }

    void Refresh()
    {
        _root.Q<Button>("SubmenuHistorial")?.AddToClassList("nav-submenu-item--active");
        _btnAction.text = "DASHBOARD";

        if (_myTeam != null)
        {
            if (_headerTeamName != null)
                _headerTeamName.text = _myTeam.name.ToUpper();
            if (_headerManagerName != null)
                _headerManagerName.text = $"Manager: {_manager?.name ?? ""}";
            if (_headerTeamLogo != null)
            {
                var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64/");
                foreach (var s in logos)
                {
                    if (s.name == _myTeam.logo)
                    {
                        _headerTeamLogo.style.backgroundImage = new StyleBackground(s);
                        break;
                    }
                }
            }
        }

        if (_season != null)
        {
            _root.Q<Label>("HeaderSeason").text = $"Temporada {_season.year_start}-{_season.year_end}";
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        BuildHistorial();
    }

    void BuildHistorial()
    {
        _historialBody.Clear();

        if (_trades == null || _trades.Count == 0)
        {
            var empty = new VisualElement();
            empty.AddToClassList("historial-empty");
            var lbl = new Label("No hay traspasos esta temporada.");
            lbl.AddToClassList("historial-empty-text");
            empty.Add(lbl);
            _historialBody.Add(empty);
            return;
        }

        var sorted = _trades.OrderByDescending(t => t.game_day).ToList();

        foreach (var trade in sorted)
        {
            var row = BuildTradeRow(trade);
            _historialBody.Add(row);
        }
    }

    VisualElement BuildTradeRow(TradeData trade)
    {
        var row = new VisualElement();
        row.AddToClassList("historial-row");

        _playerCache.TryGetValue(trade.player_id, out var player);

        // Col 1: Date
        var dateLabel = new Label();
        dateLabel.AddToClassList("historial-col");
        dateLabel.AddToClassList("historial-col-first");
        try
        {
            dateLabel.text = System.DateTime.Parse(trade.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            dateLabel.text = trade.game_date ?? "";
        }
        row.Add(dateLabel);

        // Col 2: Player name
        var playerLabel = new Label();
        playerLabel.AddToClassList("historial-col");
        playerLabel.AddToClassList("historial-col-player");
        playerLabel.text = player != null ? $"{player.first_name} {player.last_name}" : $"ID {trade.player_id}";
        row.Add(playerLabel);

        // Col 3: Seller logo
        var sellerLogo = new VisualElement();
        sellerLogo.AddToClassList("historial-col");
        sellerLogo.AddToClassList("historial-col-logo");
        TeamData fromTeam = null;
        if (trade.trade_type != "free_agent")
            _teamCache.TryGetValue(trade.team_id_from, out fromTeam);
        if (fromTeam != null)
        {
            var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32/");
            foreach (var s in logos)
            {
                if (s.name == fromTeam.logo)
                {
                    sellerLogo.style.backgroundImage = new StyleBackground(s);
                    break;
                }
            }
        }
        row.Add(sellerLogo);

        // Col 4: Seller name
        var sellerName = new Label();
        sellerName.AddToClassList("historial-col");
        if (trade.trade_type != "free_agent" && fromTeam != null)
            sellerName.text = fromTeam.name;
        else
            sellerName.text = "";
        row.Add(sellerName);

        // Col 5: Buyer logo
        var buyerLogo = new VisualElement();
        buyerLogo.AddToClassList("historial-col");
        buyerLogo.AddToClassList("historial-col-logo");
        if (_teamCache.TryGetValue(trade.team_id_to, out var toTeam))
        {
            var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32/");
            foreach (var s in logos)
            {
                if (s.name == toTeam.logo)
                {
                    buyerLogo.style.backgroundImage = new StyleBackground(s);
                    break;
                }
            }
        }
        row.Add(buyerLogo);

        // Col 6: Buyer name
        var buyerName = new Label();
        buyerName.AddToClassList("historial-col");
        buyerName.text = toTeam != null ? toTeam.name : $"ID {trade.team_id_to}";
        row.Add(buyerName);

        // Col 7: Position
        var posLabel = new Label();
        posLabel.AddToClassList("historial-col");
        posLabel.AddToClassList("historial-col-center");
        posLabel.AddToClassList("historial-col-bold");
        posLabel.text = player?.position ?? "";
        row.Add(posLabel);

        // Col 8: Overall
        var ovrLabel = new Label();
        ovrLabel.AddToClassList("historial-col");
        ovrLabel.AddToClassList("historial-col-center");
        ovrLabel.AddToClassList("historial-col-bold");
        ovrLabel.AddToClassList("historial-col-ovr");
        ovrLabel.text = player?.overall.ToString() ?? "";
        row.Add(ovrLabel);

        return row;
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });
    }

    void RegisterNavButtons()
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
        _root.Q<Button>("SubmenuEmpleados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Employees); });
        _root.Q<Button>("SubmenuLesionados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Injured); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Palmares); });
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Records); });
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
        _root.Q<Button>("SubmenuCartera")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Cartera);
        });
        _root.Q<Button>("SubmenuHistorial")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Historial);
        });
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
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
