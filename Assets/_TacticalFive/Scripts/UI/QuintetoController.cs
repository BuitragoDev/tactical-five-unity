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
    private VisualElement _selectedCard;

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

    static readonly string[] PosOrder = { "PG", "SG", "SF", "PF", "C" };

    static readonly Dictionary<string, (float x, float y)> CourtPositions = new()
    {
        {"PG", (50f, 80f)},
        {"SF", (20f, 52f)},
        {"SG", (75f, 62f)},
        {"PF", (25f, 25f)},
        {"C",  (60f, 15f)},
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
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
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
            { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Cartera); });
        _root.Q<Button>("SubmenuHistorial")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Historial); });
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

        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(_btnAction);
    }

    void Refresh()
    {
        RefreshHeader();
        _root.Q<Button>("SubmenuQuinteto")?.AddToClassList("nav-submenu-item--active");
        EnsureLineupSeeded();
        _selectedPlayer = null;
        _selectedCard = null;
        HidePlayerDetail();
        BuildLineup();
    }

    void EnsureLineupSeeded()
    {
        var existing = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        if (existing.Count == 0)
            DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, _players);
        else
        {
            var assigned = new HashSet<int>(existing.Select(l => l.player_id));
            foreach (var p in _players)
            {
                if (!assigned.Contains(p.id))
                    DatabaseManager.Instance.SetPlayerSlot(p.id, _myTeam.id, 2);
            }
        }
        _lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

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

        int chem = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        _headerChemistry.text = $"{chem}%";
        _headerChemistry.style.color = chem >= 70 ? new StyleColor(new Color(39f / 255, 174f / 255, 96f / 255)) :
                                         chem >= 40 ? new StyleColor(new Color(212f / 255, 160f / 255, 23f / 255)) :
                                         new StyleColor(new Color(192f / 255, 57f / 255, 43f / 255));

        _btnAction.text = "DASHBOARD";
    }

    void BuildLineup()
    {
        _benchList.Clear();
        _inactiveList.Clear();

        var lineupByPlayer = _lineup.ToDictionary(l => l.player_id);
        var starterPlayers = new List<PlayerData>();
        var benchPlayers = new List<PlayerData>();
        var inactivePlayers = new List<PlayerData>();

        foreach (var p in _players)
        {
            if (!lineupByPlayer.TryGetValue(p.id, out var ls))
            {
                inactivePlayers.Add(p);
                continue;
            }
            if (ls.slot == 0)
                starterPlayers.Add(p);
            else if (ls.slot == 1)
                benchPlayers.Add(p);
            else
                inactivePlayers.Add(p);
        }

        starterPlayers = starterPlayers
            .OrderBy(p =>
            {
                var ls = lineupByPlayer[p.id];
                if (ls.slot_index >= 0 && ls.slot_index < PosOrder.Length)
                    return (0, ls.slot_index);
                int fallbackIdx = Array.IndexOf(PosOrder, p.position);
                return (1, fallbackIdx >= 0 ? fallbackIdx : 999);
            })
            .ToList();

        for (int i = 0; i < PosOrder.Length; i++)
        {
            var slot = _startersList.Q<VisualElement>($"StarterSlot{PosOrder[i]}");
            if (slot == null) continue;

            slot.Clear();
            slot.AddToClassList("starter-slot");

            var label = new Label();
            label.AddToClassList("starter-slot-label");
            label.text = PosOrder[i];
            slot.Add(label);

            if (i < starterPlayers.Count)
            {
                var card = CreatePlayerCard(starterPlayers[i], 0);
                slot.Add(card);
                slot.RemoveFromClassList("starter-slot--empty");

                var ls = lineupByPlayer[starterPlayers[i].id];
                if (ls.slot_index != i)
                    DatabaseManager.Instance.SetPlayerSlot(starterPlayers[i].id, _myTeam.id, 0, i);
            }
            else
            {
                slot.AddToClassList("starter-slot--empty");
            }
        }

        benchPlayers = benchPlayers
            .OrderBy(p =>
            {
                var ls = lineupByPlayer[p.id];
                return ls.slot_index >= 0 ? ls.slot_index : 999;
            })
            .ThenByDescending(p => p.overall)
            .ToList();

        for (int bi = 0; bi < benchPlayers.Count; bi++)
        {
            var p = benchPlayers[bi];
            var card = CreatePlayerCard(p, 1);
            _benchList.Add(card);
            var ls = lineupByPlayer[p.id];
            if (ls.slot_index != bi)
                DatabaseManager.Instance.SetPlayerSlot(p.id, _myTeam.id, 1, bi);
        }

        inactivePlayers = inactivePlayers
            .OrderBy(p =>
            {
                var ls = lineupByPlayer[p.id];
                return ls.slot_index >= 0 ? ls.slot_index : 999;
            })
            .ThenByDescending(p => p.overall)
            .ToList();

        for (int ii = 0; ii < inactivePlayers.Count; ii++)
        {
            var p = inactivePlayers[ii];
            var card = CreatePlayerCard(p, 2);
            _inactiveList.Add(card);
            var ls = lineupByPlayer[p.id];
            if (ls.slot_index != ii)
                DatabaseManager.Instance.SetPlayerSlot(p.id, _myTeam.id, 2, ii);
        }

        BuildCourtView();
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
    }

    void ShowPlayerDetail(PlayerData p)
    {
        _detailEmpty.style.display = DisplayStyle.None;
        _detailScroll.style.display = DisplayStyle.Flex;

        _detailPosBadge.text = p.position;
        _detailPlayerName.text = $"{p.first_name} {p.last_name}".ToUpper();
        _detailPlayerMeta.text = $"{PlayerAge(p)} años · {p.nationality}";
        _detailOvr.text = p.overall.ToString();

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

        if (player.injury_days > 0)
            card.AddToClassList("player-card--injured");

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
        posLbl.text = $"{player.position} - ";
        meta.Add(posLbl);

        var ovrLbl = new Label();
        ovrLbl.AddToClassList("player-card-ovr");
        ovrLbl.text = $" {player.overall} OVR";
        meta.Add(ovrLbl);

        info.Add(meta);
        card.Add(info);

        card.RegisterCallback<PointerDownEvent>(_ =>
        {
            PlayClick();

            if (_selectedPlayer == null)
            {
                _selectedPlayer = player;
                _selectedCard = card;
                card.AddToClassList("player-card--selected");
            }
            else if (_selectedPlayer.id == player.id)
            {
                _selectedCard?.RemoveFromClassList("player-card--selected");
                _selectedPlayer = null;
                _selectedCard = null;
            }
            else
            {
                var srcLineup = _lineup.FirstOrDefault(l => l.player_id == _selectedPlayer.id);
                var tgtLineup = _lineup.FirstOrDefault(l => l.player_id == player.id);
                if (srcLineup != null && tgtLineup != null)
                {
                    int srcSlot = srcLineup.slot;
                    int tgtSlot = tgtLineup.slot;
                    int srcIdx = srcLineup.slot_index;
                    int tgtIdx = tgtLineup.slot_index;

                    if (srcSlot == tgtSlot)
                    {
                        DatabaseManager.Instance.SetPlayerSlot(_selectedPlayer.id, _myTeam.id, srcSlot, tgtIdx);
                        DatabaseManager.Instance.SetPlayerSlot(player.id, _myTeam.id, tgtSlot, srcIdx);
                    }
                    else
                    {
                        DatabaseManager.Instance.SetPlayerSlot(_selectedPlayer.id, _myTeam.id, tgtSlot, tgtSlot == 0 ? tgtIdx : -1);
                        DatabaseManager.Instance.SetPlayerSlot(player.id, _myTeam.id, srcSlot, srcSlot == 0 ? srcIdx : -1);
                    }
                }

                _selectedCard?.RemoveFromClassList("player-card--selected");
                _selectedPlayer = null;
                _selectedCard = null;

                _lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
                BuildLineup();
            }
        });

        card.RegisterCallback<PointerEnterEvent>(_ => ShowPlayerDetail(player));
        card.RegisterCallback<PointerLeaveEvent>(_ => HidePlayerDetail());

        return card;
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
