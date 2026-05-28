using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class ArenaController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private VisualElement _arenaInfoBody;
    private VisualElement _upgradesBody;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private TeamSettingsData _teamSettings;

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
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _arenaInfoBody = _root.Q<VisualElement>("ArenaInfoBody");
        _upgradesBody = _root.Q<VisualElement>("UpgradesBody");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _teamSettings = DatabaseManager.Instance.GetTeamSettings(_myTeam.id);
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ => ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("BtnReset")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.MainMenu));
    }

    void RegisterNavButtons()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Roster));
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Calendar));
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Standings));
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Palmares));
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Results));
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Playoffs));
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Stats));
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Records));
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Market));
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Finances));
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Sponsors));
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.TV));
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Messages));
    }

    void Refresh()
    {
        RefreshHeader();
        BuildArenaInfo();
        BuildUpgrades();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos) logoDict[s.name] = s;

        if (logoDict.TryGetValue(_myTeam.logo, out var sprite))
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
            var nextGame = DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id);
            _root.Q<Label>("HeaderDate").text = nextGame != null
                ? System.DateTime.Parse(nextGame.game_date).ToString("dd/MM/yyyy") : "";
        }

        _btnAction.text = "← DASHBOARD";
    }

    void BuildArenaInfo()
    {
        _arenaInfoBody.Clear();

        var settings = _teamSettings ?? new TeamSettingsData();

        AddDetailRow(_arenaInfoBody, "NOMBRE", settings.arena_name ?? _myTeam.name);
        AddDetailRow(_arenaInfoBody, "CAPACIDAD", $"{settings.arena_capacity:N0}");
        AddDetailRow(_arenaInfoBody, "ASISTENCIA MEDIA", $"{settings.avg_attendance:N0}");
        AddDetailRow(_arenaInfoBody, "PRECIO ENTRADA", $"${settings.ticket_price:N2}");
        AddDetailRow(_arenaInfoBody, "NIVEL", $"{GetArenaLevel(settings.arena_level)}");
    }

    void BuildUpgrades()
    {
        _upgradesBody.Clear();

        var settings = _teamSettings ?? new TeamSettingsData();
        var currentLevel = settings.arena_level;

        var upgrades = new List<(string name, string desc, int cost, int newLevel)>();

        if (currentLevel < 5)
        {
            int cost = GetUpgradeCost(currentLevel);
            upgrades.Add(($"EXPANDIR PABELLÓN (NIVEL {currentLevel + 1})",
                $"Aumenta capacidad en 2,000 asientos",
                cost, currentLevel + 1));
        }

        if (settings.ticket_price < 200)
        {
            upgrades.Add(("AUMENTAR PRECIO ENTRADAS",
                $"Incrementar precio a ${settings.ticket_price + 10:F2}",
                0, settings.arena_level));
        }

        foreach (var (name, desc, cost, newLevel) in upgrades)
        {
            var item = new VisualElement();
            item.AddToClassList("upgrade-item");

            var info = new VisualElement();
            info.AddToClassList("upgrade-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("upgrade-name");
            nameLbl.text = name;

            var descLbl = new Label();
            descLbl.AddToClassList("upgrade-desc");
            descLbl.text = desc;

            info.Add(nameLbl);
            info.Add(descLbl);

            var costLbl = new Label();
            costLbl.AddToClassList("upgrade-cost");
            costLbl.text = cost > 0 ? $"${cost:N0}" : "GRATIS";

            var upgradeBtn = new Button();
            upgradeBtn.AddToClassList("btn-upgrade");
            upgradeBtn.text = "MEJORAR";

            var upgradeCopy = (cost, newLevel);
            upgradeBtn.clicked += () => PerformUpgrade(upgradeCopy.cost, upgradeCopy.newLevel);

            item.Add(info);
            item.Add(costLbl);
            item.Add(upgradeBtn);

            _upgradesBody.Add(item);
        }
    }

    void AddDetailRow(VisualElement parent, string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("arena-detail-row");

        var lbl = new Label();
        lbl.AddToClassList("arena-detail-label");
        lbl.text = label;

        var val = new Label();
        val.AddToClassList("arena-detail-value");
        val.text = value;

        row.Add(lbl);
        row.Add(val);

        parent.Add(row);
    }

    void PerformUpgrade(int cost, int newLevel)
    {
        if (cost > 0 && _myTeam.budget < cost)
        {
            Debug.LogWarning("Presupuesto insuficiente para esta mejora");
            return;
        }

        var settings = _teamSettings ?? new TeamSettingsData();

        if (cost > 0)
        {
            _myTeam.budget -= cost;
            DatabaseManager.Instance.UpdateTeamBudget(_myTeam.id, _myTeam.budget);

            var finance = new FinanceRecord
            {
                team_id = _myTeam.id,
                season_id = _season.id,
                game_day = DatabaseManager.Instance.GetCurrentDay(_manager.id),
                record_type = 5,
                amount = cost,
                created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            DatabaseManager.Instance.AddFinanceRecord(finance);
        }

        settings.arena_level = newLevel;
        settings.arena_capacity += 2000;
        settings.arena_name = settings.arena_name ?? _myTeam.name;

        DatabaseManager.Instance.UpdateTeamSettings(settings);

        LoadData();
        Refresh();
    }

    string GetArenaLevel(int level)
    {
        return level switch
        {
            1 => "BÁSICO",
            2 => "ESTÁNDAR",
            3 => "AVANZADO",
            4 => "PREMIUM",
            5 => "ÉLITE",
            _ => "DESCONOCIDO"
        };
    }

    int GetUpgradeCost(int currentLevel)
    {
        return currentLevel switch
        {
            0 => 5_000_000,
            1 => 10_000_000,
            2 => 20_000_000,
            3 => 40_000_000,
            4 => 80_000_000,
            _ => 100_000_000
        };
    }
}
