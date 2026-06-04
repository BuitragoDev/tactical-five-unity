using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class MarketController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private Button _tabFreeAgents;
    private Button _tabTrades;
    private VisualElement _freeAgentsPanel;
    private VisualElement _tradesPanel;
    private VisualElement _freeAgentsBody;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<PlayerData> _freeAgents;

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
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _tabFreeAgents = _root.Q<Button>("TabFreeAgents");
        _tabTrades = _root.Q<Button>("TabTrades");
        _freeAgentsPanel = _root.Q<VisualElement>("FreeAgentsPanel");
        _tradesPanel = _root.Q<VisualElement>("TradesPanel");
        _freeAgentsBody = _root.Q<VisualElement>("FreeAgentsBody");
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _freeAgents = DatabaseManager.Instance.GetFreeAgents();
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();
        _tabFreeAgents?.RegisterCallback<ClickEvent>(_ => ShowTab("freeagents"));
        _tabTrades?.RegisterCallback<ClickEvent>(_ => ShowTab("trades"));
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
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Finances));
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Sponsors));
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.TV));
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Arena));
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Messages));
    }

    void Refresh()
    {
        RefreshHeader();
        BuildFreeAgents();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
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
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetNextGameDateString(_manager.id, _myTeam.id);
        }

        _btnAction.text = "← DASHBOARD";
    }

    void ShowTab(string tab)
    {
        _tabFreeAgents.RemoveFromClassList("market-tab--active");
        _tabTrades.RemoveFromClassList("market-tab--active");

        if (tab == "freeagents")
        {
            _tabFreeAgents.AddToClassList("market-tab--active");
            _freeAgentsPanel.style.display = DisplayStyle.Flex;
            _tradesPanel.style.display = DisplayStyle.None;
        }
        else
        {
            _tabTrades.AddToClassList("market-tab--active");
            _freeAgentsPanel.style.display = DisplayStyle.None;
            _tradesPanel.style.display = DisplayStyle.Flex;
        }
    }

    void BuildFreeAgents()
    {
        _freeAgentsBody.Clear();

        foreach (var player in _freeAgents)
        {
            var row = new VisualElement();
            row.AddToClassList("market-player-row");

            var posLbl = new Label();
            posLbl.AddToClassList("market-player-pos");
            posLbl.text = player.position;

            var nameLbl = new Label();
            nameLbl.AddToClassList("market-player-name");
            nameLbl.text = $"{player.first_name} {player.last_name}";

            var ovrLbl = new Label();
            ovrLbl.AddToClassList("market-player-ovr");
            ovrLbl.text = player.overall.ToString();

            var metaLbl = new Label();
            metaLbl.AddToClassList("market-player-meta");
            metaLbl.text = $"{player.age} años · {player.height_cm / 100f:F2}m";

            var salaryLbl = new Label();
            salaryLbl.AddToClassList("market-player-salary");
            salaryLbl.text = $"${player.salary / 1_000_000}M";

            var btnSign = new Button();
            btnSign.AddToClassList("market-player-action");
            btnSign.text = "FICHAR";
            int playerId = player.id;
            btnSign.RegisterCallback<ClickEvent>(_ => OnSignPlayer(playerId));

            row.Add(posLbl);
            row.Add(nameLbl);
            row.Add(ovrLbl);
            row.Add(metaLbl);
            row.Add(salaryLbl);
            row.Add(btnSign);

            _freeAgentsBody.Add(row);
        }
    }

    void OnSignPlayer(int playerId)
    {
        var player = _freeAgents.Find(p => p.id == playerId);
        if (player == null) return;

        player.team_id = _myTeam.id;
        DatabaseManager.Instance.UpdatePlayer(player);

        Debug.Log($"[Market] {_myTeam.name} ficha a {player.first_name} {player.last_name}");

        _freeAgents = DatabaseManager.Instance.GetFreeAgents();
        BuildFreeAgents();
    }
}
