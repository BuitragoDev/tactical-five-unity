using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class MessagesController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private VisualElement _messagesBody;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<MessageData> _messages;

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
        _btnAction = _root.Q<Button>("BtnAction");
        _messagesBody = _root.Q<VisualElement>("MessagesBody");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _messages = DatabaseManager.Instance.GetMessages(_manager.id);
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
    }

    void RegisterNavButtons()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var btn = _root.Q<Button>("NavRoster");
            if (btn != null)
                btn.EnableInClassList("nav-item--expanded", !btn.ClassListContains("nav-item--expanded"));
        });
        _root.Q<Button>("SubmenuJugadores")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<Button>("NavRoster")?.RemoveFromClassList("nav-item--expanded");
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        _root.Q<Button>("SubmenuEmpleados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); });
        _root.Q<Button>("SubmenuLesionados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); });
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
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Market); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Finances); });
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavConfig")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });
    }

    void LoadSidebarIcons()
    {
        var iconMap = new System.Collections.Generic.Dictionary<string, string>
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
            {"NavConfigIcon", "configuracion"}
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

    void Refresh()
    {
        RefreshHeader();
        BuildMessages();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos) logoDict[s.name] = s;

        if (logoDict.TryGetValue(_myTeam.logo, out var sprite))
            _root.Q<VisualElement>("HeaderTeamLogo").style.backgroundImage = new StyleBackground(sprite);

        _root.Q<Label>("HeaderTeamName").text = _myTeam.name.ToUpper();
        _root.Q<Label>("HeaderManagerName").text = $"Manager: {_manager.name}";
        var budgetLabel = _root.Q<Label>("HeaderBudget");
        budgetLabel.text = $"${_myTeam.budget / 1_000_000}M";
        budgetLabel.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

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
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "DASHBOARD";
    }

    void BuildMessages()
    {
        _messagesBody.Clear();

        if (_messages == null || _messages.Count == 0)
        {
            var empty = new VisualElement();
            empty.AddToClassList("messages-empty");
            var lbl = new Label("No hay mensajes en la bandeja de entrada.");
            lbl.AddToClassList("messages-empty-text");
            empty.Add(lbl);
            _messagesBody.Add(empty);
            return;
        }

        // Sort by date descending (newest first)
        var sorted = _messages.OrderByDescending(m => m.created_at).ToList();

        foreach (var message in sorted)
        {
            var card = CreateMessageCard(message);
            _messagesBody.Add(card);

            // Mark as read when viewing
            if (message.is_read == 0)
                DatabaseManager.Instance.MarkMessageRead(message.id);
        }
    }

    VisualElement CreateMessageCard(MessageData message)
    {
        var card = new VisualElement();
        card.AddToClassList("message-card");
        if (message.is_read == 0)
            card.AddToClassList("message-card--unread");

        // Header: title + delete button
        var header = new VisualElement();
        header.AddToClassList("message-card-header");

        var title = new Label(message.title);
        title.AddToClassList("message-card-title");
        header.Add(title);

        var deleteBtn = new Button();
        deleteBtn.AddToClassList("message-card-delete");
        var trashTex = Resources.Load<Texture2D>("Icons/papelera");
        if (trashTex != null)
            deleteBtn.style.backgroundImage = new StyleBackground(trashTex);
        var msgId = message.id;
        deleteBtn.clicked += () => { PlayClick(); DeleteMessage(msgId); };
        header.Add(deleteBtn);

        card.Add(header);

        // Date
        var date = new Label();
        date.AddToClassList("message-card-date");
        try
        {
            date.text = System.DateTime.Parse(message.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            date.text = message.game_date ?? "";
        }
        card.Add(date);

        // Body
        var body = new Label(message.body);
        body.AddToClassList("message-card-body");
        card.Add(body);

        return card;
    }

    void DeleteMessage(int messageId)
    {
        DatabaseManager.Instance.DeleteMessage(messageId);
        LoadData();
        BuildMessages();
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
