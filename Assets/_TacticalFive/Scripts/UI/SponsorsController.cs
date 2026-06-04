using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class SponsorsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private VisualElement _currentSponsorBanner;
    private Label _currentSponsorName;
    private VisualElement _cardsContainer;
    private Label _infoMessage;

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
        _currentSponsorBanner = _root.Q<VisualElement>("CurrentSponsorBanner");
        _currentSponsorName = _root.Q<Label>("CurrentSponsorName");
        _cardsContainer = _root.Q<VisualElement>("SponsorsCardsContainer");
        _infoMessage = _root.Q<Label>("SponsorsInfoMessage");
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
        BuildCurrentSponsorBanner();
        BuildCards();
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
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetNextGameDateString(_manager.id, _myTeam.id);
        }

        _btnAction.text = "← DASHBOARD";
    }

    void BuildCurrentSponsorBanner()
    {
        if (_currentSponsor != null)
        {
            _currentSponsorBanner.style.display = DisplayStyle.Flex;
            _currentSponsorName.text = _currentSponsor.name;
        }
        else
        {
            _currentSponsorBanner.style.display = DisplayStyle.None;
        }
    }

    void BuildCards()
    {
        _cardsContainer.Clear();

        if (_availableSponsors == null || _availableSponsors.Count == 0)
        {
            var emptyLbl = new Label("No hay patrocinadores disponibles.");
            emptyLbl.AddToClassList("sponsors-info-message");
            _cardsContainer.Add(emptyLbl);
            return;
        }

        bool hasCurrent = _currentSponsor != null;

        foreach (var sponsor in _availableSponsors)
        {
            var card = CreateCard(sponsor, hasCurrent);
            _cardsContainer.Add(card);
        }
    }

    VisualElement CreateCard(SponsorData sponsor, bool hasCurrent)
    {
        var card = new VisualElement();
        card.AddToClassList("sponsor-card");

        // Logo
        var logo = new VisualElement();
        logo.AddToClassList("sponsor-card-logo");
        // Load sponsor logo from Resources (strip .png extension for Resources.Load)
        var logoPath = sponsor.logo?.Replace(".png", "");
        var sponsorLogo = Resources.Load<Sprite>(logoPath);
        if (sponsorLogo != null)
            logo.style.backgroundImage = new StyleBackground(sponsorLogo);

        // If we have a current sponsor and this is not it, show in grayscale
        if (hasCurrent && _currentSponsor != null && sponsor.id != _currentSponsor.id)
            logo.AddToClassList("sponsor-card-logo--grayscale");

        card.Add(logo);

        // Name
        var nameLbl = new Label(sponsor.name.ToUpper());
        nameLbl.AddToClassList("sponsor-card-name");
        card.Add(nameLbl);

        // Ingreso Inicial
        card.Add(CreateCardRow("Ingreso Inicial", $"${sponsor.initial_income:N0}"));

        // Por Partido en Casa
        card.Add(CreateCardRow("Por Partido en Casa", $"${sponsor.home_game_income:N0}"));

        // Duración
        card.Add(CreateCardRow("Duración", $"{sponsor.contract_years} año{(sponsor.contract_years > 1 ? "s" : "")}"));

        // Button
        var btn = new Button();
        btn.AddToClassList("sponsor-card-btn");
        bool isContracted = hasCurrent && _currentSponsor != null && _currentSponsor.id == sponsor.id;

        if (isContracted)
        {
            btn.text = "CONTRATADO";
            btn.AddToClassList("sponsor-card-btn--disabled");
            btn.SetEnabled(false);
        }
        else if (hasCurrent)
        {
            btn.text = "CONTRATADO";
            btn.AddToClassList("sponsor-card-btn--disabled");
            btn.SetEnabled(false);
        }
        else
        {
            btn.text = "CONTRATAR";
            var sponsorCopy = sponsor;
            btn.clicked += () => SignSponsor(sponsorCopy);
        }
        card.Add(btn);

        return card;
    }

    VisualElement CreateCardRow(string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("sponsor-card-row");

        var lbl = new Label(label);
        lbl.AddToClassList("sponsor-card-label");

        var val = new Label(value);
        val.AddToClassList("sponsor-card-value");

        row.Add(lbl);
        row.Add(val);

        return row;
    }

    void SignSponsor(SponsorData sponsor)
    {
        if (_currentSponsor != null) return; // Can't sign if already have one

        DatabaseManager.Instance.SignSponsor(sponsor.id, _season.id, _myTeam.id, _season.current_game_day);

        // Send message
        var msg = new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"PATROCINADOR FIRMADO: {sponsor.name.ToUpper()}",
            body = $"Se ha firmado un nuevo patrocinio con {sponsor.name}.\n\nIngreso inicial: ${sponsor.initial_income:N0}",
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        };
        DatabaseManager.Instance.AddMessage(msg);

        LoadData();
        Refresh();
    }
}
