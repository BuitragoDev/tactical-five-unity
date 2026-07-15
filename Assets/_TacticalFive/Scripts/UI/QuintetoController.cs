using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

public class QuintetoController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerChemistry;
    private Label _headerSeason;
    private Label _headerDate;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Button _btnAction;

    private VisualElement _startersList;
    private VisualElement _benchList;
    private VisualElement _inactiveList;
    private VisualElement _courtContainer;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<PlayerData> _players;
    private List<LineupData> _lineup;

    private Dictionary<string, Sprite> _logoSprites = new();

    private PlayerData _selectedPlayer;
    private VisualElement _selectedSlot;

    private VisualElement _detailEmpty;
    private ScrollView _detailScroll;
    private VisualElement _detailContent;
    private Label _detailPosBadge;
    private Label _detailPlayerName;
    private Label _detailPlayerMeta;
    private Label _detailOvr;
    private VisualElement _detailAttrs;
    private Label _statPts;
    private Label _statReb;
    private Label _statAst;
    private Label _statStl;
    private Label _statBlk;

    const int BENCH_SLOTS = 7;
    const int INACTIVE_SLOTS = 5;
    static readonly string[] PosOrder = { "PG", "SG", "SF", "PF", "C" };

    static readonly Dictionary<string, (float x, float y)> CourtPositions = new()
    {
        {"PG", (50f, 80f)},
        {"SF", (20f, 52f)},
        {"SG", (75f, 62f)},
        {"PF", (25f, 25f)},
        {"C",  (60f, 15f)},
    };
    // Config modal
    private VisualElement _configModalOverlay;
    private VisualElement _configModalBox;
    private Button _btnConfigCerrar;
    private CustomSlider _configSliderMaster;
    private CustomSlider _configSliderMusic;
    private CustomSlider _configSliderSFX;
    private Label _configLabelMaster;
    private Label _configLabelMusic;
    private Label _configLabelSFX;
    private Button _configBtnQualityLow;
    private Button _configBtnQualityMedium;
    private Button _configBtnQualityHigh;
    private Button _configBtnQualityUltra;

    // Config confirm modals
    private VisualElement _configMainMenuConfirmOverlay;
    private Button _configBtnMainMenu;
    private Button _configBtnMainMenuYes;
    private Button _configBtnMainMenuNo;
    private VisualElement _configExitConfirmOverlay;
    private Button _configBtnExit;
    private Button _configBtnExitYes;
    private Button _configBtnExitNo;



    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        CursorManager.Instance?.SetDefaultCursor();
        CacheReferences();
        LoadSidebarIcons();
        LoadData();
        RegisterCallbacks();
        InitConfigModal();
        Refresh();
    }

    void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerChemistry = _root.Q<Label>("HeaderChemistry");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _btnAction = _root.Q<Button>("BtnAction");

        _startersList = _root.Q<VisualElement>("StartersList");
        _benchList = _root.Q<VisualElement>("BenchList");
        _inactiveList = _root.Q<VisualElement>("InactiveList");
        _courtContainer = _root.Q<VisualElement>("CourtContainer");

        _detailEmpty = _root.Q<VisualElement>("DetailEmpty");
        _detailScroll = _root.Q<ScrollView>("DetailScroll");
        _detailContent = _root.Q<VisualElement>("DetailContent");
        _detailPosBadge = _root.Q<Label>("DetailPosBadge");
        _detailPlayerName = _root.Q<Label>("DetailPlayerName");
        _detailPlayerMeta = _root.Q<Label>("DetailPlayerMeta");
        _detailOvr = _root.Q<Label>("DetailOvr");
        _detailAttrs = _root.Q<VisualElement>("DetailAttrs");
        _statPts = _root.Q<Label>("StatPts");
        _statReb = _root.Q<Label>("StatReb");
        _statAst = _root.Q<Label>("StatAst");
        _statStl = _root.Q<Label>("StatStl");
        _statBlk = _root.Q<Label>("StatBlk");
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
            {"NavManagerIcon", "manager"},
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
    }

    void RegisterCallbacks()
    {
        // Sidebar unificado
        SidebarController.Attach(_root, GameScreen.Quinteto);
        HeaderController.Attach(_root);
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        var allSubmenus = new[] {
            _root.Q<VisualElement>("RosterSubmenu"),
            _root.Q<VisualElement>("PalmaresSubmenu"),
            _root.Q<VisualElement>("MarketSubmenu"),
            _root.Q<VisualElement>("FinanceSubmenu")
        };

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
        _root.Q<Button>("SubmenuQuinteto")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Quinteto);
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
        _root.Q<Button>("NavManager")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Manager); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OpenConfigModal(); });

        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnAction);
            CursorManager.Instance.RegisterHandCursor(_root.Q<VisualElement>("ConfigIcon"));

            var navNames = new[] {
                "NavDashboard", "NavRoster", "NavCalendar", "NavStandings",
                "NavPalmares", "NavResults", "NavPlayoffs", "NavStats",
                "NavMarket", "NavFinances", "NavArena", "NavManager", "NavMessages",
                "SubmenuJugadores", "SubmenuQuinteto", "SubmenuEntrenamiento",
                "SubmenuEmpleados", "SubmenuLesionados",
                "SubmenuPalmares", "SubmenuRecords",
                "SubmenuOfertas", "SubmenuCartera", "SubmenuHistorial",
                "SubmenuDecisiones", "SubmenuPrestamos", "SubmenuSponsors", "SubmenuTV"
            };
            foreach (var name in navNames)
            {
                var el = _root.Q<VisualElement>(name);
                if (el != null)
                    CursorManager.Instance.RegisterHandCursor(el);
            }
        }
    }

    void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Quinteto] RefreshHeader error: {ex.Message}"); }
        _root.Q<Button>("SubmenuQuinteto")?.AddToClassList("nav-submenu-item--active");
        EnsureLineupSeeded();
        _selectedPlayer = null;
        _selectedSlot = null;
        HidePlayerDetail();
        BuildLineup();
    }

    void EnsureLineupSeeded()
    {
        var existing = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        if (existing.Count == 0)
        {
            DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, _players);
        }
        else
        {
            var currentIds = new HashSet<int>(_players.Select(p => p.id));
            foreach (var e in existing)
            {
                if (!currentIds.Contains(e.player_id))
                    DatabaseManager.Instance.DeleteLineupEntry(e.id);
            }
            existing = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
            var assigned = new HashSet<int>(existing.Select(l => l.player_id));
            int nextInactIdx = existing.Where(l => l.slot == 2).Select(l => l.slot_index).DefaultIfEmpty(-1).Max() + 1;
            foreach (var p in _players)
            {
                if (!assigned.Contains(p.id))
                {
                    if (nextInactIdx < INACTIVE_SLOTS)
                    {
                        DatabaseManager.Instance.SetPlayerSlot(p.id, _myTeam.id, 2, nextInactIdx);
                        nextInactIdx++;
                    }
                }
            }
        }
        _lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;
        if (_headerTeamName == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

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
        _headerMargin.text = marginText;
        _headerMargin.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) _headerMargin.AddToClassList("header-stat-value--negative");

        int chem = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        _headerChemistry.text = $"{chem}%";
        _headerChemistry.style.color = chem >= 70 ? new StyleColor(new Color(39f / 255, 174f / 255, 96f / 255)) :
                                         chem >= 40 ? new StyleColor(new Color(212f / 255, 160f / 255, 23f / 255)) :
                                         new StyleColor(new Color(192f / 255, 57f / 255, 43f / 255));

        _btnAction.text = "MENÚ PRINCIPAL";
    }

    void BuildLineup()
    {
        _benchList.Clear();
        _inactiveList.Clear();

        for (int i = 0; i < PosOrder.Length; i++)
        {
            var slot = _startersList.Q<VisualElement>($"StarterSlot{PosOrder[i]}");
            if (slot == null) continue;

            slot.Clear();
            slot.RemoveFromClassList("slot--injured");
            slot.AddToClassList("starter-slot");

            var label = new Label();
            label.AddToClassList("starter-slot-label");
            label.text = PositionCodes.GetShort(PosOrder[i]);
            slot.Add(label);

            var ls = _lineup.FirstOrDefault(l => l.slot == 0 && l.slot_index == i);
            var p = ls != null ? _players.FirstOrDefault(pl => pl.id == ls.player_id) : null;
            if (p != null)
            {
                var card = CreatePlayerCard(p, 0);
                slot.Add(card);
                slot.RemoveFromClassList("starter-slot--empty");
                if (p.injury_days > 0) slot.AddToClassList("slot--injured");
            }
            else
            {
                slot.AddToClassList("starter-slot--empty");
            }
            slot.Add(CreateTransferButton(0, i));
        }

        for (int bi = 0; bi < BENCH_SLOTS; bi++)
        {
            var slot = new VisualElement();
            slot.AddToClassList("bench-slot");

            var ls = _lineup.FirstOrDefault(l => l.slot == 1 && l.slot_index == bi);
            var p = ls != null ? _players.FirstOrDefault(pl => pl.id == ls.player_id) : null;
            if (p != null)
            {
                var card = CreatePlayerCard(p, 1);
                slot.Add(card);
                if (p.injury_days > 0) slot.AddToClassList("slot--injured");
            }
            else
            {
                slot.AddToClassList("bench-slot--empty");
            }
            slot.Add(CreateTransferButton(1, bi));

            _benchList.Add(slot);
        }

        for (int ii = 0; ii < INACTIVE_SLOTS; ii++)
        {
            var slot = new VisualElement();
            slot.AddToClassList("inactive-slot");

            var ls = _lineup.FirstOrDefault(l => l.slot == 2 && l.slot_index == ii);
            var p = ls != null ? _players.FirstOrDefault(pl => pl.id == ls.player_id) : null;
            if (p != null)
            {
                var card = CreatePlayerCard(p, 2);
                slot.Add(card);
                if (p.injury_days > 0) slot.AddToClassList("slot--injured");
            }
            else
            {
                slot.AddToClassList("inactive-slot--empty");
            }
            slot.Add(CreateTransferButton(2, ii));

            _inactiveList.Add(slot);
        }

        BuildCourtView();
    }

    Button CreateTransferButton(int targetSlot, int targetSlotIndex)
    {
        var btn = new Button();
        btn.AddToClassList("slot-transfer-btn");
        var tex = Resources.Load<Texture2D>("Icons/intercambio");
        if (tex != null)
            btn.style.backgroundImage = new StyleBackground(tex);
        else
            btn.text = "\u21C4";

        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();

            var tgtLineup = _lineup.FirstOrDefault(l => l.slot == targetSlot && l.slot_index == targetSlotIndex);
            var tgtPlayer = tgtLineup != null ? _players.FirstOrDefault(p => p.id == tgtLineup.player_id) : null;

            if (tgtPlayer == null)
            {
                if (_selectedPlayer != null)
                {
                    _selectedSlot?.RemoveFromClassList("slot--selected");
                    DatabaseManager.Instance.SetPlayerSlot(_selectedPlayer.id, _myTeam.id, targetSlot, targetSlotIndex);
                    _selectedPlayer = null;
                    _selectedSlot = null;
                    _lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
                    BuildLineup();
                }
                return;
            }

            var slotEl = btn.parent;
            if (_selectedPlayer == null)
            {
                _selectedPlayer = tgtPlayer;
                _selectedSlot = slotEl;
                slotEl.AddToClassList("slot--selected");
            }
            else if (_selectedPlayer.id == tgtPlayer.id)
            {
                slotEl.RemoveFromClassList("slot--selected");
                _selectedPlayer = null;
                _selectedSlot = null;
            }
            else
            {
                _selectedSlot?.RemoveFromClassList("slot--selected");
                var srcLineup = _lineup.FirstOrDefault(l => l.player_id == _selectedPlayer.id);
                if (srcLineup != null)
                {
                    int srcSlot = srcLineup.slot;
                    int srcIdx = srcLineup.slot_index;

                    DatabaseManager.Instance.SetPlayerSlot(_selectedPlayer.id, _myTeam.id, targetSlot, targetSlotIndex);
                    DatabaseManager.Instance.SetPlayerSlot(tgtPlayer.id, _myTeam.id, srcSlot, srcIdx);
                }

                _selectedPlayer = null;
                _selectedSlot = null;
                _lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
                BuildLineup();
            }
        });

        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(btn);
        return btn;
    }

    void BuildCourtView()
    {
        _courtContainer.Clear();

        var courtImg = Resources.Load<Texture2D>("Icons/pista_basket");
        if (courtImg != null)
            _courtContainer.style.backgroundImage = new StyleBackground(courtImg);

        for (int i = 0; i < PosOrder.Length; i++)
        {
            var pos = PosOrder[i];
            if (!CourtPositions.TryGetValue(pos, out var posPct)) continue;

            var slot = _startersList.Q<VisualElement>($"StarterSlot{pos}");
            string displayName = pos;

            if (slot != null && slot.childCount > 1 && slot[1].userData is (PlayerData pd, int))
                displayName = $"{pd.first_name} {pd.last_name}";

            var courtCard = new VisualElement();
            courtCard.AddToClassList("court-card");
            courtCard.style.left = new Length(posPct.x, LengthUnit.Percent);
            courtCard.style.top = new Length(posPct.y, LengthUnit.Percent);

            var nameLbl = new Label();
            nameLbl.AddToClassList("court-card-name");
            nameLbl.text = displayName;
            courtCard.Add(nameLbl);

            _courtContainer.Add(courtCard);
        }

        // Remove old auto button if any
        _courtContainer.parent.Q<Button>("AutoLineupBtn")?.RemoveFromHierarchy();

        var autoBtn = new Button();
        autoBtn.name = "AutoLineupBtn";
        autoBtn.AddToClassList("auto-lineup-btn");
        autoBtn.text = "CONVOCATORIA AUTOM\u00c1TICA";
        autoBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var injuredIds = new HashSet<int>(_players
                .Where(p => p.injury_days > 0)
                .Select(p => p.id));
            DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, _players, injuredIds);
            _selectedPlayer = null;
            _selectedSlot = null;
            _lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
            BuildLineup();
        });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(autoBtn);
        _courtContainer.parent.Add(autoBtn);
    }

    void ShowPlayerDetail(PlayerData p)
    {
        _detailEmpty.style.display = DisplayStyle.None;
        _detailScroll.style.display = DisplayStyle.Flex;

        _detailPosBadge.text = string.IsNullOrEmpty(p.secondary_position)
            ? PositionCodes.GetName(p.position)
            : $"{PositionCodes.GetName(p.position)} / {PositionCodes.GetName(p.secondary_position)}";
        _detailPlayerName.text = $"{p.first_name} {p.last_name}".ToUpper();
        _detailPlayerMeta.text = $"{PlayerAge(p)} años · {CountryCodes.GetName(p.nationality)}";
        _detailOvr.text = p.GetCalculatedAverage().ToString();

        BuildAttrBars(p);

        var s = DatabaseManager.Instance.GetPlayerSeasonStats(p.id, _manager.id);
        _statPts.text = s.avgPts.ToString("F1");
        _statReb.text = s.avgReb.ToString("F1");
        _statAst.text = s.avgAst.ToString("F1");
        _statStl.text = s.avgStl.ToString("F1");
        _statBlk.text = s.avgBlk.ToString("F1");
    }

    void HidePlayerDetail()
    {
        _detailEmpty.style.display = DisplayStyle.Flex;
        _detailScroll.style.display = DisplayStyle.None;
    }

    int PlayerAge(PlayerData p)
    {
        return p.age;
    }

    void BuildAttrBars(PlayerData p)
    {
        _detailAttrs.Clear();

        var attrs = new[]
        {
            ("TIRO",      p.shooting),
            ("TRIPLE",    p.three_point),
            ("PASE",      p.passing),
            ("BOTE",      p.dribbling),
            ("DEFENSA",   p.defense),
            ("REBOTE",    p.rebounding),
            ("VELOCIDAD", p.speed),
            ("ATLETISMO", p.athleticism),
            ("IQ",        p.iq),
            ("ROBOS",     p.steals),
            ("TAPONES",   p.blocks),
            ("MORAL",     p.morale),
        };

        foreach (var (label, val) in attrs)
        {
            var row = new VisualElement();
            row.AddToClassList("attr-row");

            var lbl = new Label();
            lbl.AddToClassList("attr-label");
            lbl.text = label;

            var barBg = new VisualElement();
            barBg.AddToClassList("attr-bar-bg");

            var barFill = new VisualElement();
            barFill.AddToClassList("attr-bar-fill");
            if (val < 50) barFill.AddToClassList("attr-bar-fill--low");
            else if (val < 70) barFill.AddToClassList("attr-bar-fill--mid");

            barFill.style.width = new StyleLength(new Length(val, LengthUnit.Percent));
            barBg.Add(barFill);

            var valLbl = new Label();
            valLbl.AddToClassList("attr-val");
            valLbl.text = val.ToString();

            row.Add(lbl);
            row.Add(barBg);
            row.Add(valLbl);
            _detailAttrs.Add(row);
        }
    }

    VisualElement CreatePlayerCard(PlayerData player, int slot)
    {
        var card = new VisualElement();
        card.AddToClassList("player-card");
        card.userData = (player, slot);

        switch (slot)
        {
            case 0: card.AddToClassList("player-card--starter"); break;
            case 1: card.AddToClassList("player-card--bench"); break;
            case 2: card.AddToClassList("player-card--inactive"); break;
        }

        // injured class goes on the parent slot, not the card

        var avatar = new VisualElement();
        avatar.AddToClassList("player-card-avatar");
        Texture2D tex = PlayerPhotoHelper.Load(player.id, player.photo);
        if (tex != null)
            avatar.style.backgroundImage = new StyleBackground(tex);
        card.Add(avatar);

        var info = new VisualElement();
        info.AddToClassList("player-card-info");

        var nameLbl = new Label();
        nameLbl.AddToClassList("player-card-name");
        nameLbl.text = $"{player.first_name} {player.last_name}";
        info.Add(nameLbl);

        var meta = new VisualElement();
        meta.AddToClassList("player-card-meta");

        var posLbl = new Label();
        posLbl.AddToClassList("player-card-pos");
        var posStr = PositionCodes.GetShort(player.position);
        if (!string.IsNullOrEmpty(player.secondary_position))
            posStr += $" / {PositionCodes.GetShort(player.secondary_position)}";
        posLbl.text = $"{posStr} - ";
        meta.Add(posLbl);

        var ovrLbl = new Label();
        ovrLbl.AddToClassList("player-card-ovr");
        ovrLbl.text = $" {player.GetCalculatedAverage()} MED";
        meta.Add(ovrLbl);

        info.Add(meta);
        card.Add(info);

        card.RegisterCallback<PointerEnterEvent>(_ => ShowPlayerDetail(player));
        card.RegisterCallback<PointerLeaveEvent>(_ => HidePlayerDetail());

        return card;
    }
    void InitConfigModal()
    {
        _configModalOverlay = _root.Q<VisualElement>("ConfigModalOverlay");
        _configModalBox     = _root.Q<VisualElement>("ConfigModalBox");
        _btnConfigCerrar    = _root.Q<Button>("ConfigBtnCerrar");

        _configSliderMaster = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderMaster"),
            _root.Q<VisualElement>("ConfigFillMaster"),
            _root.Q<VisualElement>("ConfigDraggerMaster"));
        _configSliderMusic  = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderMusic"),
            _root.Q<VisualElement>("ConfigFillMusic"),
            _root.Q<VisualElement>("ConfigDraggerMusic"));
        _configSliderSFX    = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderSFX"),
            _root.Q<VisualElement>("ConfigFillSFX"),
            _root.Q<VisualElement>("ConfigDraggerSFX"));
        _configLabelMaster  = _root.Q<Label>("ConfigLabelMaster");
        _configLabelMusic   = _root.Q<Label>("ConfigLabelMusic");
        _configLabelSFX     = _root.Q<Label>("ConfigLabelSFX");
        _configBtnQualityLow    = _root.Q<Button>("ConfigBtnQualityLow");
        _configBtnQualityMedium = _root.Q<Button>("ConfigBtnQualityMedium");
        _configBtnQualityHigh   = _root.Q<Button>("ConfigBtnQualityHigh");
        _configBtnQualityUltra  = _root.Q<Button>("ConfigBtnQualityUltra");

        _configBtnMainMenu     = _root.Q<Button>("ConfigBtnMainMenu");
        _configBtnExit         = _root.Q<Button>("ConfigBtnExit");

        _configMainMenuConfirmOverlay = _root.Q<VisualElement>("ConfigMainMenuConfirmOverlay");
        _configBtnMainMenuYes = _root.Q<Button>("ConfigBtnMainMenuYes");
        _configBtnMainMenuNo  = _root.Q<Button>("ConfigBtnMainMenuNo");

        _configExitConfirmOverlay = _root.Q<VisualElement>("ConfigExitConfirmOverlay");
        _configBtnExitYes = _root.Q<Button>("ConfigBtnExitYes");
        _configBtnExitNo  = _root.Q<Button>("ConfigBtnExitNo");

        _configSliderMaster.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMasterVolume(v);
            UpdateConfigLabels();
        };
        _configSliderMusic.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMusicVolume(v);
            UpdateConfigLabels();
        };
        _configSliderSFX.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetSFXVolume(v);
            UpdateConfigLabels();
        };

        _configBtnQualityLow?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(0); });
        _configBtnQualityMedium?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(1); });
        _configBtnQualityHigh?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(2); });
        _configBtnQualityUltra?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(3); });

        _btnConfigCerrar?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseConfigModal(); });
        _configModalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configModalOverlay)
                { PlayClick(); CloseConfigModal(); }
        });

        _configBtnMainMenu?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenMainMenuConfirmModal(); });
        _configBtnExit?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenExitConfirmModal(); });

        _configBtnMainMenuYes?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        _configBtnMainMenuNo?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseMainMenuConfirmModal();
        });
        _configMainMenuConfirmOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configMainMenuConfirmOverlay)
                { PlayClick(); CloseMainMenuConfirmModal(); }
        });

        _configBtnExitYes?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            QuitGame();
        });
        _configBtnExitNo?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseExitConfirmModal();
        });
        _configExitConfirmOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configExitConfirmOverlay)
                { PlayClick(); CloseExitConfirmModal(); }
        });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnConfigCerrar);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityLow);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityMedium);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityHigh);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityUltra);
            CursorManager.Instance.RegisterHandCursor(_configBtnMainMenu);
            CursorManager.Instance.RegisterHandCursor(_configBtnExit);
            CursorManager.Instance.RegisterHandCursor(_configBtnMainMenuYes);
            CursorManager.Instance.RegisterHandCursor(_configBtnMainMenuNo);
            CursorManager.Instance.RegisterHandCursor(_configBtnExitYes);
            CursorManager.Instance.RegisterHandCursor(_configBtnExitNo);

            if (_configSliderMaster?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderMaster.Container);
            if (_configSliderMusic?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderMusic.Container);
            if (_configSliderSFX?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderSFX.Container);
        }
    }

    void OpenConfigModal()
    {
        CursorManager.Instance?.SetDefaultCursor();
        var am = AudioManager.Instance;
        if (am != null)
        {
            _configSliderMaster.SetValueWithoutNotify(am.MasterVolume);
            _configSliderMusic.SetValueWithoutNotify(am.MusicVolume);
            _configSliderSFX.SetValueWithoutNotify(am.SFXVolume);
            UpdateConfigLabels();
        }
        int q = QualitySettings.GetQualityLevel();
        UpdateConfigQualityButtons(Mathf.Clamp(q, 0, 3));

        _configModalOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configModalOverlay.AddToClassList("modal-overlay--visible");
        _configModalBox.AddToClassList("modal-box--visible");
    }

    void CloseConfigModal()
    {
        _configModalOverlay.RemoveFromClassList("modal-overlay--visible");
        _configModalBox.RemoveFromClassList("modal-box--visible");
    }

    void UpdateConfigLabels()
    {
        var am = AudioManager.Instance;
        if (am == null) return;
        if (_configLabelMaster != null)
            _configLabelMaster.text = $"{Mathf.RoundToInt(am.MasterVolume * 100)}%";
        if (_configLabelMusic != null)
            _configLabelMusic.text  = $"{Mathf.RoundToInt(am.MusicVolume  * 100)}%";
        if (_configLabelSFX != null)
            _configLabelSFX.text    = $"{Mathf.RoundToInt(am.SFXVolume    * 100)}%";
    }

    void SelectConfigQuality(int index)
    {
        AudioManager.Instance?.SetQualityLevel(index);
        UpdateConfigQualityButtons(index);
    }

    void UpdateConfigQualityButtons(int activeIndex)
    {
        var buttons = new[] { _configBtnQualityLow, _configBtnQualityMedium, _configBtnQualityHigh, _configBtnQualityUltra };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].EnableInClassList("settings-quality-btn--active", i == activeIndex);
        }
    }

    void OpenMainMenuConfirmModal()
    {
        CloseConfigModal();
        PlayClick();
        _configMainMenuConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configMainMenuConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseMainMenuConfirmModal()
    {
        _configMainMenuConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.RemoveFromClassList("modal-box--visible");
        OpenConfigModal();
    }

    void OpenExitConfirmModal()
    {
        CloseConfigModal();
        PlayClick();
        _configExitConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configExitConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseExitConfirmModal()
    {
        _configExitConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.RemoveFromClassList("modal-box--visible");
        OpenConfigModal();
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }



    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
