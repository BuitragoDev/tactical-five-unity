using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class SponsorsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private VisualElement _currentSponsorBody;
    private Label _noSponsorText;
    private VisualElement _availableSponsorsBody;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private SponsorData _currentSponsor;
    private List<SponsorData> _availableSponsors;

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
        _currentSponsorBody = _root.Q<VisualElement>("CurrentSponsorBody");
        _noSponsorText = _root.Q<Label>("NoSponsorText");
        _availableSponsorsBody = _root.Q<VisualElement>("AvailableSponsorsBody");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _currentSponsor = DatabaseManager.Instance.GetActiveSponsor(_myTeam.id);
        _availableSponsors = DatabaseManager.Instance.GetAvailableSponsors(_myTeam.id);
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
        BuildCurrentSponsor();
        BuildAvailableSponsors();
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

    void BuildCurrentSponsor()
    {
        _currentSponsorBody.Clear();

        if (_currentSponsor == null)
        {
            _noSponsorText.style.display = DisplayStyle.Flex;
            return;
        }

        _noSponsorText.style.display = DisplayStyle.None;

        var nameLbl = new Label();
        nameLbl.AddToClassList("sponsor-current-name");
        nameLbl.text = _currentSponsor.name.ToUpper();

        var detailsLbl = new Label();
        detailsLbl.AddToClassList("sponsor-current-details");
        detailsLbl.text = $"Tipo: {GetSponsorTypeName(_currentSponsor.sponsor_type)}";

        var valueLbl = new Label();
        valueLbl.AddToClassList("sponsor-current-value");
        valueLbl.text = $"${_currentSponsor.value:N0} / temporada";

        _currentSponsorBody.Add(nameLbl);
        _currentSponsorBody.Add(detailsLbl);
        _currentSponsorBody.Add(valueLbl);
    }

    void BuildAvailableSponsors()
    {
        _availableSponsorsBody.Clear();

        foreach (var sponsor in _availableSponsors)
        {
            var item = new VisualElement();
            item.AddToClassList("sponsor-item");

            var info = new VisualElement();
            info.AddToClassList("sponsor-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("sponsor-name");
            nameLbl.text = sponsor.name.ToUpper();

            var detailsLbl = new Label();
            detailsLbl.AddToClassList("sponsor-details");
            detailsLbl.text = $"Tipo: {GetSponsorTypeName(sponsor.sponsor_type)}";

            info.Add(nameLbl);
            info.Add(detailsLbl);

            var valueLbl = new Label();
            valueLbl.AddToClassList("sponsor-value");
            valueLbl.text = $"${sponsor.value:N0}";

            var signBtn = new Button();
            signBtn.AddToClassList("btn-sign-sponsor");
            signBtn.text = "FIRMAR";

            var sponsorCopy = sponsor;
            signBtn.clicked += () => SignSponsor(sponsorCopy);

            item.Add(info);
            item.Add(valueLbl);
            item.Add(signBtn);

            _availableSponsorsBody.Add(item);
        }
    }

    void SignSponsor(SponsorData sponsor)
    {
        if (_currentSponsor != null)
        {
            DatabaseManager.Instance.FireSponsor(_currentSponsor.id, _season.id, _myTeam.id);
        }

        DatabaseManager.Instance.SignSponsor(sponsor.id, _season.id, _myTeam.id);

        var finance = new FinanceRecord
        {
            team_id = _myTeam.id,
            season_id = _season.id,
            game_day = DatabaseManager.Instance.GetCurrentDay(_manager.id),
            record_type = 3,
            amount = sponsor.value,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        DatabaseManager.Instance.AddFinanceRecord(finance);

        var msg = new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"PATROCINADOR FIRMADO: {sponsor.name.ToUpper()}",
            body = $"Se ha firmado un nuevo patrocinio con {sponsor.name}.\n\nValor: ${sponsor.value:N0} por temporada.",
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        };
        DatabaseManager.Instance.AddMessage(msg);

        LoadData();
        Refresh();
    }

    string GetSponsorTypeName(int type)
    {
        return type switch
        {
            1 => "LOCAL",
            2 => "REGIONAL",
            3 => "NACIONAL",
            4 => "INTERNACIONAL",
            _ => "OTRO"
        };
    }
}
