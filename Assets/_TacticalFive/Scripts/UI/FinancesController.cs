using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class FinancesController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private Label _totalIncome;
    private Label _totalExpenses;
    private Label _balance;
    private VisualElement _financeRecordsBody;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<FinanceRecord> _financeRecords;

    private static readonly Dictionary<int, string> TypeLabels = new()
    {
        { 1, "TAQUILLA" },
        { 2, "ABONOS" },
        { 3, "PATROCINIOS" },
        { 4, "TELEVISIÓN" },
        { 5, "REMODELACIÓN" },
        { 6, "DESPIDO" },
        { 7, "SUELDOS" }
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
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _totalIncome = _root.Q<Label>("TotalIncome");
        _totalExpenses = _root.Q<Label>("TotalExpenses");
        _balance = _root.Q<Label>("Balance");
        _financeRecordsBody = _root.Q<VisualElement>("FinanceRecordsBody");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _financeRecords = _season != null
            ? DatabaseManager.Instance.GetFinanceRecords(_myTeam.id, _season.id)
            : new List<FinanceRecord>();
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
        BuildSummary();
        BuildRecords();
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

    void BuildSummary()
    {
        long income = DatabaseManager.Instance.GetTotalIncome(_myTeam.id, _season.id);
        long expenses = DatabaseManager.Instance.GetTotalExpenses(_myTeam.id, _season.id);
        long bal = income - expenses;

        _totalIncome.text = $"+${income / 1_000_000}M";
        _totalExpenses.text = $"-${expenses / 1_000_000}M";
        _balance.text = bal >= 0 ? $"+${bal / 1_000_000}M" : $"-${Mathf.Abs((int)(bal / 1_000_000))}M";
        _balance.RemoveFromClassList("finance-card-value--income");
        _balance.RemoveFromClassList("finance-card-value--expense");
        _balance.AddToClassList(bal >= 0 ? "finance-card-value--income" : "finance-card-value--expense");
    }

    void BuildRecords()
    {
        _financeRecordsBody.Clear();

        foreach (var record in _financeRecords)
        {
            var row = new VisualElement();
            row.AddToClassList("finance-record-row");

            var typeLbl = new Label();
            typeLbl.AddToClassList("finance-record-type");
            typeLbl.text = TypeLabels.TryGetValue(record.record_type, out var label) ? label : "OTRO";

            var dayLbl = new Label();
            dayLbl.AddToClassList("finance-record-day");
            dayLbl.text = $"Día {record.game_day}";

            var amountLbl = new Label();
            amountLbl.AddToClassList("finance-record-amount");
            bool isIncome = record.record_type <= 4;
            amountLbl.AddToClassList(isIncome ? "finance-record-amount--income" : "finance-record-amount--expense");
            amountLbl.text = isIncome ? $"+${record.amount:N0}" : $"-${record.amount:N0}";

            row.Add(typeLbl);
            row.Add(dayLbl);
            row.Add(amountLbl);

            _financeRecordsBody.Add(row);
        }
    }
}
