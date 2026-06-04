using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class TVController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private VisualElement _currentTVBanner;
    private Label _currentTVName;
    private VisualElement _cardsContainer;
    private Label _infoMessage;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private TvChannelData _currentTV;
    private List<TvChannelData> _availableChannels;

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
        _currentTVBanner = _root.Q<VisualElement>("CurrentTVBanner");
        _currentTVName = _root.Q<Label>("CurrentTVName");
        _cardsContainer = _root.Q<VisualElement>("TVCardsContainer");
        _infoMessage = _root.Q<Label>("TVInfoMessage");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _currentTV = DatabaseManager.Instance.GetActiveTVChannel(_myTeam.id);
        _availableChannels = DatabaseManager.Instance.GetAvailableTVChannels(_myTeam.id);
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
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Arena));
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Messages));
    }

    void Refresh()
    {
        RefreshHeader();
        BuildCurrentTVBanner();
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

    void BuildCurrentTVBanner()
    {
        if (_currentTV != null)
        {
            _currentTVBanner.style.display = DisplayStyle.Flex;
            _currentTVName.text = _currentTV.name;
        }
        else
        {
            _currentTVBanner.style.display = DisplayStyle.None;
        }
    }

    void BuildCards()
    {
        _cardsContainer.Clear();

        if (_availableChannels == null || _availableChannels.Count == 0)
        {
            var emptyLbl = new Label("No hay cadenas de televisión disponibles.");
            emptyLbl.AddToClassList("tv-info-message");
            _cardsContainer.Add(emptyLbl);
            return;
        }

        bool hasCurrent = _currentTV != null;

        foreach (var channel in _availableChannels)
        {
            var card = CreateCard(channel, hasCurrent);
            _cardsContainer.Add(card);
        }
    }

    VisualElement CreateCard(TvChannelData channel, bool hasCurrent)
    {
        var card = new VisualElement();
        card.AddToClassList("tv-card");

        // Logo
        var logo = new VisualElement();
        logo.AddToClassList("tv-card-logo");
        var logoPath = channel.logo?.Replace(".png", "");
        var channelLogo = Resources.Load<Sprite>(logoPath);
        if (channelLogo != null)
            logo.style.backgroundImage = new StyleBackground(channelLogo);

        // If we have a current TV channel and this is not it, show in grayscale
        if (hasCurrent && _currentTV != null && channel.id != _currentTV.id)
            logo.AddToClassList("tv-card-logo--grayscale");

        card.Add(logo);

        // Name
        var nameLbl = new Label(channel.name.ToUpper());
        nameLbl.AddToClassList("tv-card-name");
        card.Add(nameLbl);

        // Ingreso Inicial
        card.Add(CreateCardRow("Ingreso Inicial", $"${channel.initial_income:N0}"));

        // Por Partido en Casa
        card.Add(CreateCardRow("Por Partido en Casa", $"${channel.home_game_income:N0}"));

        // Duración
        card.Add(CreateCardRow("Duración", $"{channel.contract_years} año{(channel.contract_years > 1 ? "s" : "")}"));

        // Button
        var btn = new Button();
        btn.AddToClassList("tv-card-btn");
        bool isContracted = hasCurrent && _currentTV != null && channel.id == _currentTV.id;

        if (isContracted)
        {
            btn.text = "CONTRATADO";
            btn.AddToClassList("tv-card-btn--disabled");
            btn.SetEnabled(false);
        }
        else if (hasCurrent)
        {
            btn.text = "CONTRATADO";
            btn.AddToClassList("tv-card-btn--disabled");
            btn.SetEnabled(false);
        }
        else
        {
            btn.text = "CONTRATAR";
            var channelCopy = channel;
            btn.clicked += () => SignTV(channelCopy);
        }
        card.Add(btn);

        return card;
    }

    VisualElement CreateCardRow(string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("tv-card-row");

        var lbl = new Label(label);
        lbl.AddToClassList("tv-card-label");

        var val = new Label(value);
        val.AddToClassList("tv-card-value");

        row.Add(lbl);
        row.Add(val);

        return row;
    }

    void SignTV(TvChannelData channel)
    {
        if (_currentTV != null) return; // Can't sign if already have one

        DatabaseManager.Instance.SignTVChannel(channel.id, _season.id, _myTeam.id, _season.current_game_day);

        // Send message
        var msg = new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"CADENA TV FIRMADA: {channel.name.ToUpper()}",
            body = $"Se ha firmado un nuevo contrato con {channel.name}.\n\nIngreso inicial: ${channel.initial_income:N0}",
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        };
        DatabaseManager.Instance.AddMessage(msg);

        LoadData();
        Refresh();
    }
}
