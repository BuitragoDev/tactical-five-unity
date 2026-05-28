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
    private VisualElement _messageDetail;
    private Button _btnBackToInbox;
    private Label _messageSubject;
    private Label _messageDate;
    private Label _messageBody;

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
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _messagesBody = _root.Q<VisualElement>("MessagesBody");
        _messageDetail = _root.Q<VisualElement>("MessageDetail");
        _btnBackToInbox = _root.Q<Button>("BtnBackToInbox");
        _messageSubject = _root.Q<Label>("MessageSubject");
        _messageDate = _root.Q<Label>("MessageDate");
        _messageBody = _root.Q<Label>("MessageBody");
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
        _btnAction?.RegisterCallback<ClickEvent>(_ => ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _btnBackToInbox?.RegisterCallback<ClickEvent>(_ => ShowInbox());
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
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Arena));
    }

    void Refresh()
    {
        RefreshHeader();
        BuildMessages();
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

    void BuildMessages()
    {
        _messagesBody.Clear();

        foreach (var message in _messages)
        {
            var item = new VisualElement();
            item.AddToClassList("message-item");
            if (message.is_read == 0)
                item.AddToClassList("message-item--unread");

            var senderLbl = new Label();
            senderLbl.AddToClassList("message-sender");
            senderLbl.text = GetSenderName(message.sender_type, message.sender_id);

            var subjectLbl = new Label();
            subjectLbl.AddToClassList("message-subject-preview");
            subjectLbl.text = message.title;

            var dateLbl = new Label();
            dateLbl.AddToClassList("message-date-preview");
            dateLbl.text = System.DateTime.Parse(message.created_at).ToString("dd/MM");

            item.Add(senderLbl);
            item.Add(subjectLbl);
            item.Add(dateLbl);

            var msgCopy = message;
            item.RegisterCallback<ClickEvent>(_ => OpenMessage(msgCopy));

            _messagesBody.Add(item);
        }
    }

    void OpenMessage(MessageData message)
    {
        if (message.is_read == 0)
        {
            DatabaseManager.Instance.MarkMessageRead(message.id);
        }

        _messageSubject.text = message.title;
        _messageDate.text = System.DateTime.Parse(message.created_at).ToString("dd/MM/yyyy HH:mm");
        _messageBody.text = message.body;

        _messagesBody.style.display = DisplayStyle.None;
        _messageDetail.style.display = DisplayStyle.Flex;
    }

    void ShowInbox()
    {
        _messagesBody.style.display = DisplayStyle.Flex;
        _messageDetail.style.display = DisplayStyle.None;
        LoadData();
        BuildMessages();
    }

    string GetSenderName(int senderType, int senderId)
    {
        return senderType switch
        {
            1 => "SISTEMA",
            2 => "GM",
            3 => "PRENSA",
            4 => "AGENTE",
            _ => "DESCONOCIDO"
        };
    }
}
